using Microsoft.EntityFrameworkCore;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Data; // Ваш DbContext, нужно добавить
using System.Linq;

namespace Monitoring.Infrastructure.Services
{
    public class WorkItemAppService : IWorkItemAppService
    {
        private readonly MyDbContext _context;

        public WorkItemAppService(MyDbContext context)
        {
            _context = context;
        }

        public async Task<List<WorkItemDto>> GetWorkItemsByDivisionAsync(int divisionId)
        {
            // Допустим, у нас есть сущности Works, Documents, TypeDocs, WorkUsers, Users и т.д.
            // Здесь мы выбираем все работы, где исполнитель (User) относится к нужному divisionId.
            // Пример LINQ-запроса (упрощённый).
            var rawQuery =
                from w in _context.Works
                join doc in _context.Documents on w.IdDocuments equals doc.Id
                join td in _context.TypeDocs on doc.IdTypeDoc equals td.Id
                join wu in _context.WorkUsers on w.Id equals wu.IdWork
                join uExec in _context.Users on wu.IdUser equals uExec.IdUser
                // LEFT JOIN WorkUserCheck
                join wuc in _context.WorkUserChecks
                    //.Where(x => x.IdWork == w.Id)
                    .DefaultIfEmpty()
                    on w.Id equals wuc.IdWork into wucJoin
                from wuc in wucJoin.DefaultIfEmpty()

                    // LEFT JOIN Users (для Approver = userCheck)
                join userCheck in _context.Users
                    .Where(xx => xx.IdDivision == divisionId)  // иногда фильтр, но можно не делать
                    .DefaultIfEmpty()
                    on wuc.IdUser equals userCheck.IdUser into userCheckJoin
                from userCheck in userCheckJoin.DefaultIfEmpty()

                    // LEFT JOIN WorkUserControl
                join wcontr in _context.WorkUserControls
                    //.Where(x => x.IdWork == w.Id)
                    .DefaultIfEmpty()
                    on w.Id equals wcontr.IdWork into wcontrJoin
                from wcontr in wcontrJoin.DefaultIfEmpty()

                    // LEFT JOIN Users (для Controller = userContr)
                join userContr in _context.Users
                    .Where(xx => xx.IdDivision == divisionId) // либо без фильтра
                    .DefaultIfEmpty()
                    on wcontr.IdUser equals userContr.IdUser into userContrJoin
                from userContr in userContrJoin.DefaultIfEmpty()

                    // Фильтруем по тому, чтобы сам исполнитель (uExec) принадлежал данному отделу.
                where uExec.IdDivision == divisionId
                      // условие: WorkUser.dateFact IS NULL (если нужно)
                      && wu.DateFact == null

                select new
                {
                    WorkId = w.Id,
                    DocNumber = doc.Number,
                    DocumentName = td.Name + " " + doc.Name,
                    WorkName = w.Name,
                    Executor = uExec.SmallName,          // конкретный
                    Controller = userContr != null ? userContr.SmallName : "",
                    Approver = userCheck != null ? userCheck.SmallName : "",
                    PlanDate = w.DatePlan,
                    Korrect1 = wu.DateKorrect1,
                    Korrect2 = wu.DateKorrect2,
                    Korrect3 = wu.DateKorrect3,
                    FactDate = w.DateFact
                };

            var rawList = await rawQuery.ToListAsync();

            // Группируем, чтобы собрать исполнителей в одну строку:
            var grouped = rawList
                .GroupBy(item => new
                {
                    item.WorkId,
                    item.DocNumber,
                    item.DocumentName,
                    item.WorkName,
                    item.Controller,
                    item.Approver,
                    item.PlanDate,
                    item.Korrect1,
                    item.Korrect2,
                    item.Korrect3,
                    item.FactDate
                })
                .Select(g => new WorkItemDto
                {
                    DocumentNumber = g.Key.DocNumber + "/" + g.Key.WorkId,
                    DocumentName = g.Key.DocumentName ?? "",
                    WorkName = g.Key.WorkName ?? "",
                    Executor = string.Join(", ", g
                        .Select(x => x.Executor)
                        .Distinct()
                        .Where(x => !string.IsNullOrWhiteSpace(x))),
                    Controller = g.Key.Controller,
                    Approver = g.Key.Approver,
                    PlanDate = g.Key.PlanDate,
                    Korrect1 = g.Key.Korrect1,
                    Korrect2 = g.Key.Korrect2,
                    Korrect3 = g.Key.Korrect3,
                    FactDate = g.Key.FactDate
                })
                .OrderBy(x => x.DocumentNumber)
                .ToList();

            return grouped;
        }

        /// <summary>
        /// Метод, который учитывает фильтры (startDate, endDate, executor, approver, search).
        /// </summary>
        public async Task<List<WorkItemDto>> GetFilteredWorkItemsAsync(
            int divisionId,
            DateOnly? startDate,
            DateOnly? endDate,
            string? executor,
            string? approver,
            string? search
        )
        {
            // Берём базовый список (чтобы не дублировать код, можно вызвать предыдущий метод)
            var allForDivision = await GetWorkItemsByDivisionAsync(divisionId);

            // Теперь в памяти фильтруем по дополнительным условиям:
            // 1) По исполнителю
            if (!string.IsNullOrEmpty(executor))
            {
                allForDivision = allForDivision
                    .Where(x => x.Executor != null && x.Executor.Contains(executor, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // 2) По принимающему
            if (!string.IsNullOrEmpty(approver))
            {
                allForDivision = allForDivision
                    .Where(x => x.Approver != null && x.Approver.Contains(approver, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // 3) Поиск (DocumentName, WorkName, Executor, Controller)
            if (!string.IsNullOrEmpty(search))
            {
                allForDivision = allForDivision
                    .Where(x =>
                        (x.DocumentName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                        || (x.WorkName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                        || (x.Executor ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                        || (x.Controller ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                        || (x.Approver ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            }

            // 4) Фильтр по датам (пример: <= endDate)
            // В Razor-проекте вы делали что-то вроде: (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) <= EndDate
            // Здесь – аналогично.
            if (endDate.HasValue)
            {
                allForDivision = allForDivision
                    .Where(x =>
                        (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) <= endDate.Value
                    )
                    .ToList();
            }

            // При желании – фильтр ">= startDate"
            if (startDate.HasValue)
            {
                allForDivision = allForDivision
                    .Where(x =>
                        (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) >= startDate.Value
                    )
                    .ToList();
            }

            return allForDivision;
        }
    }
}