using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Сервис для работы с WorkItem
    /// </summary>
    public class WorkItemAppService : IWorkItemAppService
    {
        private readonly MyDbContext _context;

        public WorkItemAppService(MyDbContext context)
        {
            _context = context;
        }

        public async Task<List<WorkItemDto>> GetWorkItemsByDivisionAsync(int divisionId)
        {
            // Пример LINQ-запроса (упрощённый)
            // В реальном проекте фильтрация/запрос будут зависеть от реальной структуры БД

            var rawQuery =
                from w in _context.Works
                join doc in _context.Documents on w.IdDocuments equals doc.Id
                join wu in _context.WorkUsers on w.Id equals wu.IdWork
                join uExec in _context.Users on wu.IdUser equals uExec.IdUser
                // Контроллёр (WorkUserControl) и т.д. - опускаем для примера
                where uExec.IdDivision == divisionId
                select new
                {
                    WorkId = w.Id,
                    DocNumber = doc.Number,
                    DocumentName = doc.Name,
                    WorkName = w.Name,
                    Executor = uExec.SmallName,
                    // Можно добавить и Controller/Approver если необходимо
                    w.DatePlan,
                    wu.DateKorrect1,
                    wu.DateKorrect2,
                    wu.DateKorrect3,
                    w.DateFact
                };

            var rawList = await rawQuery.ToListAsync();

            // При необходимости – группируем, мапим в DTO
            var result = rawList
                .Select(x => new WorkItemDto
                {
                    DocumentNumber = (x.DocNumber ?? "") + "/" + x.WorkId,
                    DocumentName = x.DocumentName ?? "",
                    WorkName = x.WorkName ?? "",
                    Executor = x.Executor ?? "",
                    Controller = "", // для примера
                    Approver = "",
                    PlanDate = x.DatePlan.HasValue ? DateOnly.FromDateTime(x.DatePlan.Value) : null,
                    Korrect1 = x.DateKorrect1.HasValue ? DateOnly.FromDateTime(x.DateKorrect1.Value) : null,
                    Korrect2 = x.DateKorrect2.HasValue ? DateOnly.FromDateTime(x.DateKorrect2.Value) : null,
                    Korrect3 = x.DateKorrect3.HasValue ? DateOnly.FromDateTime(x.DateKorrect3.Value) : null,
                    FactDate = x.DateFact.HasValue ? DateOnly.FromDateTime(x.DateFact.Value) : null
                })
                .ToList();

            return result;
        }

        public async Task<List<WorkItemDto>> GetFilteredWorkItemsAsync(
            int divisionId,
            DateOnly? startDate,
            DateOnly? endDate,
            string? executor,
            string? approver,
            string? search
        )
        {
            // Сначала получаем базовый список
            var allForDivision = await GetWorkItemsByDivisionAsync(divisionId);

            // Фильтры по executor
            if (!string.IsNullOrEmpty(executor))
            {
                allForDivision = allForDivision
                    .Where(x => x.Executor.Contains(executor, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Фильтр по approver (если бы у нас было поле)
            if (!string.IsNullOrEmpty(approver))
            {
                allForDivision = allForDivision
                    .Where(x => x.Approver.Contains(approver, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Фильтр "search"
            if (!string.IsNullOrEmpty(search))
            {
                allForDivision = allForDivision
                    .Where(x =>
                        x.DocumentName.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || x.WorkName.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || x.Executor.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || x.Controller.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || x.Approver.Contains(search, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            }

            // Фильтр дат: <= endDate
            if (endDate.HasValue)
            {
                allForDivision = allForDivision
                    .Where(x =>
                        (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) <= endDate.Value
                    )
                    .ToList();
            }

            // Фильтр дат: >= startDate
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

        /// <summary>
        /// Получаем список исполнителей для указанного divisionId.
        /// Допустим, берём всех пользователей (Isvalid=1) из таблицы Users, где idDivision=divisionId
        /// </summary>
        public async Task<List<string>> GetExecutorsByDivisionId(int divisionId)
        {
            var list = await _context.Users
                .Where(u => u.IdDivision == divisionId && u.Isvalid == true)
                .Select(u => u.SmallName)
                .OrderBy(u => u)
                .ToListAsync();

            return list;
        }

        /// <summary>
        /// Получаем список "принимающих" (Approvers) – пока сделаем так же, как Executors,
        /// но в реальном проекте может быть другая логика
        /// </summary>
        public async Task<List<string>> GetApproversByDivisionId(int divisionId)
        {
            var list = await _context.Users
                .Where(u => u.IdDivision == divisionId && u.Isvalid == true)
                .Select(u => u.SmallName)
                .OrderBy(u => u)
                .ToListAsync();

            return list;
        }
    }
}