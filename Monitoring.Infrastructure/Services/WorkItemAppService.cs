using Microsoft.EntityFrameworkCore;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;
using Monitoring.Infrastructure.Data;  // пространство имён вашего DbContext
using System.Linq;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Реализация IWorkItemAppService через EF Core.
    /// </summary>
    public class WorkItemAppService : IWorkItemAppService
    {
        private readonly MyDbContext _context;

        public WorkItemAppService(MyDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Возвращаем список работ (агрегируем исполнителей и т.д.)
        /// </summary>
        public async Task<List<WorkItemDto>> GetWorkItemsByDivisionAsync(int divisionId)
        {
            // 1) Делаем JOIN-ы:
            //    WorkUser -> Works -> Documents -> TypeDocs -> (Controller/Approver через WorkUserControl/WorkUserCheck)
            //    + привязка к Users (IdUser -> smallName) при условии, что User.idDivision = divisionId
            //    Здесь упрощённо считаем, что "EXECUTOR" – это тот же User, который появляется в WorkUser.

            // Пример LINQ-запроса (с несколькими LEFT JOIN через GroupJoin/DefaultIfEmpty).
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

                // Выбираем "сырые" поля, без агрегирования исполнителей.
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

            // 2) Подгружаем всё в память (ToListAsync), после чего сгруппируем по набору полей,
            //    чтобы собрать всех исполнителей в одну строку (через string.Join).
            var rawList = await rawQuery.ToListAsync();

            // 3) Группируем:
            //    Ключевыми полями считаем: (WorkId, DocNumber, DocumentName, WorkName, Controller, Approver, PlanDate, K1,K2,K3, FactDate).
            //    А исполнителей склеиваем во "множественное" поле Executor.
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
                .OrderBy(x => x.DocumentNumber) // например, сортируем как-то
                .ToList();

            return grouped;
        }
    }
}