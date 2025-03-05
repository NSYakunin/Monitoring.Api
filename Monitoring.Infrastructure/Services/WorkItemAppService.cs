using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory; // Для кэша
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Сервис для получения списка работ (WorkItems) по отделам и фильтрам.
    /// Содержит кэширование, аналогичное старому коду.
    /// </summary>
    public class WorkItemAppService : IWorkItemAppService
    {
        private readonly MyDbContext _context;
        private readonly IMemoryCache _cache;

        public WorkItemAppService(MyDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        /// <summary>
        /// Получить все работы для конкретного подразделения (с учётом того,
        /// что user.IdDivision = divisionId).
        /// Результат кэшируем.
        /// </summary>
        public async Task<List<WorkItemDto>> GetWorkItemsByDivisionAsync(int divisionId)
        {
            // Ключ кэша:
            string cacheKey = $"WorkItems_{divisionId}";

            if (_cache.TryGetValue(cacheKey, out List<WorkItemDto> cached))
            {
                return cached;
            }

            // Если нет в кэше — загружаем из БД:
            var query =
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

            var rawList = await query.ToListAsync();

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

            // Запишем в кэш на 30 минут
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            _cache.Set(cacheKey, grouped, cacheOptions);

            return grouped;
        }

        /// <summary>
        /// Фильтруем полученный список по датам / исполнителю / принимающему / поиску.
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
            var all = await GetWorkItemsByDivisionAsync(divisionId);
            var query = all.AsQueryable();

            // Фильтр по исполнителю
            if (!string.IsNullOrEmpty(executor))
            {
                query = query.Where(x => x.Executor.Contains(executor, StringComparison.OrdinalIgnoreCase));
            }

            // Фильтр по принимающему
            if (!string.IsNullOrEmpty(approver))
            {
                query = query.Where(x => (x.Approver ?? "").Contains(approver, StringComparison.OrdinalIgnoreCase));
            }

            // Фильтр search
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x =>
                    (x.DocumentName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (x.WorkName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (x.Executor ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (x.Controller ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (x.Approver ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)
                );
            }

            // Фильтр по дате <= endDate
            if (endDate.HasValue)
            {
                query = query.Where(x =>
                    (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) <= endDate.Value
                );
            }

            // Фильтр по дате >= startDate
            if (startDate.HasValue)
            {
                query = query.Where(x =>
                    (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) >= startDate.Value
                );
            }

            return query.ToList();
        }

        /// <summary>
        /// Получаем список исполнителей для указанного divisionId.
        /// Допустим, берём всех пользователей (Isvalid=1) из таблицы Users, где idDivision=divisionId
        /// </summary>
        public async Task<List<string>> GetExecutorsByDivisionId(int divisionId)
        {
            return await _context.Users
                .Where(u => u.IdDivision == divisionId && u.Isvalid == true)
                .Select(u => u.SmallName)
                .OrderBy(u => u)
                .ToListAsync();
        }

        /// <summary>
        /// Получить список "принимающих" (в данном примере — то же самое).
        /// </summary>
        public async Task<List<string>> GetApproversByDivisionId(int divisionId)
        {
            return await _context.Users
                .Where(u => u.IdDivision == divisionId && u.Isvalid == true)
                .Select(u => u.SmallName)
                .OrderBy(u => u)
                .ToListAsync();
        }

        /// <summary>
        /// Очистить кэш для указ. division (или вообще).
        /// </summary>
        public void ClearCache(int divisionId)
        {
            string key = $"WorkItems_{divisionId}";
            _cache.Remove(key);
        }
    }
}