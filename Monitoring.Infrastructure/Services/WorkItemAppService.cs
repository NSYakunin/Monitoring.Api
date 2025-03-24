using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Monitoring.Infrastructure.Data;
using Monitoring.Infrastructure.Data.ScaffoldModels;
using Monitoring.Domain.Entities;

namespace Monitoring.Infrastructure.Services
{
    public class WorkItemAppService : IWorkItemAppService
    {
        private readonly MyDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IUserSettingsService _userSettingsService;
        private readonly IWorkRequestService _workRequestService; // Чтобы подсвечивать заявки
        private readonly string _connectionString;

        public WorkItemAppService(
            MyDbContext context,
            IMemoryCache cache,
            IUserSettingsService userSettingsService,
            IConfiguration configuration,
            IWorkRequestService workRequestService
        )
        {
            _context = context;
            _cache = cache;
            _userSettingsService = userSettingsService;
            _workRequestService = workRequestService;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string not found");
        }

        /// <summary>
        /// Основной метод: получаем постранично отфильтрованный список WorkItemsDto.
        /// </summary>
        public async Task<PagedWorkItemsDto> GetFilteredWorkItemsAsync(
            int divisionId,
            DateTime? startDate,
            DateTime? endDate,
            string? executor,
            string? approver,
            string? search,
            int userIdClaim,
            int pageNumber,
            int pageSize,
            string? currentUserName
        )
        {
            // Если divisionId == 0, значит все подразделения, 
            // определим все, доступные пользователю
            List<int> divisionsToLoad;
            if (divisionId == 0)
            {
                var userDivs = await _userSettingsService.GetUserAllowedDivisionsAsync(userIdClaim);
                // Если у пользователя совсем нет прописанных — пусть будет пусто 
                if (userDivs.Count == 0)
                    userDivs = new List<int>();

                divisionsToLoad = userDivs;
            }
            else
            {
                divisionsToLoad = new List<int> { divisionId };
            }

            if (divisionsToLoad.Count == 0)
            {
                return new PagedWorkItemsDto
                {
                    Items = new List<WorkItemDto>(),
                    CurrentPage = pageNumber,
                    PageSize = pageSize,
                    TotalPages = 1,
                    TotalCount = 0
                };
            }

            // Загружаем все работы (агрегация по нескольким отделам) 
            var all = await GetWorkItemsByDivisionAsync(divisionsToLoad);

            // Применяем фильтры
            var filtered = ApplyFilters(all, startDate, endDate, executor, approver, search);

            // Подсветка строк, если необходимо
            if (!string.IsNullOrEmpty(currentUserName))
            {
                await HighlightRows(filtered, currentUserName);
            }

            // Пагинация
            int totalCount = filtered.Count;
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            if (pageNumber < 1) pageNumber = 1;
            if (totalPages < 1) totalPages = 1; // чтобы не делить на 0
            if (pageNumber > totalPages) pageNumber = totalPages;

            var skip = (pageNumber - 1) * pageSize;
            var itemsPage = filtered.Skip(skip).Take(pageSize).ToList();

            return new PagedWorkItemsDto
            {
                Items = itemsPage,
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalCount = totalCount
            };
        }

        /// <summary>
        /// Аналогичный метод, но без пагинации (для экспорта).
        /// </summary>
        public async Task<List<WorkItemDto>> GetAllFilteredWithoutPaging(
            int divisionId,
            DateTime? startDate,
            DateTime? endDate,
            string? executor,
            string? approver,
            string? search,
            int userIdClaim,
            string? currentUserName
        )
        {
            List<int> divisionsToLoad;
            if (divisionId == 0)
            {
                var userDivs = await _userSettingsService.GetUserAllowedDivisionsAsync(userIdClaim);
                if (userDivs.Count == 0) userDivs = new List<int>();
                divisionsToLoad = userDivs;
            }
            else
            {
                divisionsToLoad = new List<int> { divisionId };
            }

            if (divisionsToLoad.Count == 0)
                return new List<WorkItemDto>();

            var all = await GetWorkItemsByDivisionAsync(divisionsToLoad);
            var filtered = ApplyFilters(all, startDate, endDate, executor, approver, search);

            if (!string.IsNullOrEmpty(currentUserName))
            {
                await HighlightRows(filtered, currentUserName);
            }

            return filtered;
        }

        /// <summary>
        /// ПОЛНОСТЬЮ (с кэшированием) получить все WorkItemsDto для списка отделов (divisionIds).
        /// - Если отделов несколько, для каждого делаем отдельный вызов GetAllWorkItemsForSingleDivision.
        /// - Далее агрегируем в один список, убираем дубли (объединяем исполнителей).
        /// </summary>
        public async Task<List<WorkItemDto>> GetWorkItemsByDivisionAsync(List<int> divisionIds)
        {
            if (divisionIds == null || divisionIds.Count == 0)
                return new List<WorkItemDto>();

            var allItems = new List<WorkItemDto>();

            // Обходим каждый отдел, тянем из кэша (или БД), складываем в общий список
            foreach (var divId in divisionIds.Distinct())
            {
                var itemsForDiv = await GetAllWorkItemsForSingleDivision(divId);
                allItems.AddRange(itemsForDiv);
            }

            // Теперь агрегируем дубликаты: если одна и та же работа, 
            // то складываем исполнителей, контролирующих и т.д.
            var dict = new Dictionary<string, WorkItemDto>();

            foreach (var w in allItems)
            {
                // Ключ, чтобы распознать ту же запись
                // используем основные поля
                string key = $"{w.DocumentName}|{w.WorkName}|{w.Approver}|{w.PlanDate}|{w.Korrect1}|{w.Korrect2}|{w.Korrect3}|{w.FactDate}|{w.DocumentNumber}";

                if (!dict.ContainsKey(key))
                {
                    dict[key] = new WorkItemDto
                    {
                        DocumentNumber = w.DocumentNumber,
                        DocumentName = w.DocumentName,
                        WorkName = w.WorkName,
                        Executor = w.Executor,
                        Controller = w.Controller,
                        Approver = w.Approver,
                        PlanDate = w.PlanDate,
                        Korrect1 = w.Korrect1,
                        Korrect2 = w.Korrect2,
                        Korrect3 = w.Korrect3,
                        FactDate = w.FactDate
                    };
                }
                else
                {
                    // Агрегируем исполнителей
                    var existing = dict[key];

                    if (!string.IsNullOrWhiteSpace(w.Executor))
                    {
                        var execList = existing.Executor
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(e => e.Trim())
                            .ToList();

                        var newExecs = w.Executor
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(e => e.Trim());

                        foreach (var ex in newExecs)
                        {
                            if (!execList.Contains(ex))
                                execList.Add(ex);
                        }
                        existing.Executor = string.Join(", ", execList);
                    }

                    // Агрегируем контролирующих
                    if (!string.IsNullOrWhiteSpace(w.Controller))
                    {
                        var ctrlList = existing.Controller
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(c => c.Trim())
                            .ToList();

                        var newCtrls = w.Controller
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(c => c.Trim());

                        foreach (var c in newCtrls)
                        {
                            if (!ctrlList.Contains(c))
                                ctrlList.Add(c);
                        }
                        existing.Controller = string.Join(", ", ctrlList);
                    }
                }
            }

            return dict.Values.ToList();
        }

        /// <summary>
        /// Приватный метод: получить все работы для ОДНОГО отдела (используем кэш).
        /// </summary>
        private async Task<List<WorkItemDto>> GetAllWorkItemsForSingleDivision(int divisionId)
        {
            // Ключ для кэша
            string cacheKey = $"AllWorkItems_div{divisionId}";

            if (_cache.TryGetValue(cacheKey, out List<WorkItemDto> cached))
                return cached;

            // Если в кэше нет, грузим из БД
            var result = new List<WorkItemDto>();
            using (var conn = new SqlConnection(_connectionString))
            {
                // Пример запроса — схож с тем, что в RazorPages, 
                // только берем исполнителя, контроллера, принимающего и т.д.
                string sql = @"
                    SELECT 
                        d.Number,
                        wu.idWork,
                        td.Name + ' ' + d.Name AS DocumentName,
                        w.Name AS WorkName,
                        (SELECT smallName FROM Users WHERE idUser = wucontr.idUser) AS Controller,
                        (SELECT smallName FROM Users WHERE idUser = wuc.idUser) AS Approver,
                        w.DatePlan AS DatePlan,
                        wu.DateKorrect1 AS DateKorrect1,
                        wu.DateKorrect2 AS DateKorrect2,
                        wu.DateKorrect3 AS DateKorrect3,
                        w.DateFact     AS DateFact,
                        -- Исполнитель:
                        u.smallName AS Executor
                    FROM WorkUser wu
                        INNER JOIN Works w ON wu.idWork = w.id
                        INNER JOIN Documents d ON w.idDocuments = d.id
                        LEFT JOIN WorkUserCheck wuc ON wuc.idWork = w.id
                        LEFT JOIN WorkUserControl wucontr ON wucontr.idWork = w.id
                        INNER JOIN TypeDocs td ON td.id = d.idTypeDoc
                        INNER JOIN Users u ON wu.idUser = u.idUser
                    WHERE wu.dateFact IS NULL
                      AND u.idDivision = @div
                      AND u.Isvalid = 1
                ";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@div", divisionId);
                    await conn.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string? idWork = reader["idWork"]?.ToString();
                            string? docNum = reader["Number"]?.ToString();
                            string docNumber = (docNum ?? "??") + "/" + (idWork ?? "??");

                            var w = new WorkItemDto
                            {
                                DocumentNumber = docNumber,
                                DocumentName = reader["DocumentName"]?.ToString() ?? "",
                                WorkName = reader["WorkName"]?.ToString() ?? "",
                                Executor = reader["Executor"]?.ToString() ?? "",
                                Controller = reader["Controller"]?.ToString() ?? "",
                                Approver = reader["Approver"]?.ToString() ?? "",
                                PlanDate = reader["DatePlan"] as DateTime?,
                                Korrect1 = reader["DateKorrect1"] as DateTime?,
                                Korrect2 = reader["DateKorrect2"] as DateTime?,
                                Korrect3 = reader["DateKorrect3"] as DateTime?,
                                FactDate = reader["DateFact"] as DateTime?
                            };
                            result.Add(w);
                        }
                    }
                }
            }

            // Запоминаем в кэше
            var cacheOpts = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            _cache.Set(cacheKey, result, cacheOpts);

            return result;
        }

        /// <summary>
        /// Фильтрация (по датам, исполнителю, принимающему, строковому поиску).
        /// </summary>
        private List<WorkItemDto> ApplyFilters(
            List<WorkItemDto> source,
            DateTime? startDate,
            DateTime? endDate,
            string? executor,
            string? approver,
            string? search
        )
        {
            var query = source.AsQueryable();

            if (!string.IsNullOrEmpty(executor))
            {
                query = query.Where(x => x.Executor != null &&
                                         x.Executor.Contains(executor, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(approver))
            {
                query = query.Where(x => (x.Approver ?? "")
                    .Contains(approver, StringComparison.OrdinalIgnoreCase));
            }
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

            if (endDate.HasValue)
            {
                query = query.Where(x => (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) <= endDate.Value);
            }

            if (startDate.HasValue)
            {
                query = query.Where(x => (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) >= startDate.Value);
            }

            return query.ToList();
        }

        /// <summary>
        /// Подсветка: если есть Pending-заявка от текущего пользователя.
        /// (ищем в IWorkRequestService)
        /// </summary>
        private async Task HighlightRows(List<WorkItemDto> items, string currentUserName)
        {
            // Для оптимизации можно сгруппировать, 
            // но покажем логику построчно (короткий пример).
            foreach (var item in items)
            {
                var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(item.DocumentNumber);
                var pendingFromMe = requests.FirstOrDefault(r =>
                    r.Status == "Pending"
                    && !r.IsDone
                    && r.Sender.Equals(currentUserName, StringComparison.OrdinalIgnoreCase)
                );
                if (pendingFromMe != null)
                {
                    if (pendingFromMe.RequestType == "факт")
                        item.HighlightCssClass = "table-info";
                    else if (pendingFromMe.RequestType.StartsWith("корр"))
                        item.HighlightCssClass = "table-warning";

                    item.UserPendingRequestId = pendingFromMe.Id;
                    item.UserPendingRequestType = pendingFromMe.RequestType;
                    item.UserPendingProposedDate = pendingFromMe.ProposedDate;
                    item.UserPendingRequestNote = pendingFromMe.Note;
                    item.UserPendingReceiver = pendingFromMe.Receiver;
                }
            }
        }

        /// <summary>
        /// Получить название отдела (smallNameDivision) из таблицы Divisions.
        /// </summary>
        public async Task<string> GetDevNameAsync(int divisionId)
        {
            string resultName = $"Отдел #{divisionId}";

            using (var conn = new SqlConnection(_connectionString))
            {
                string query = @"
                    SELECT smallNameDivision
                    FROM Divisions
                    WHERE idDivision = @divId
                ";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@divId", divisionId);
                    await conn.OpenAsync();
                    object obj = await cmd.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value)
                    {
                        string fromDb = obj.ToString();
                        if (!string.IsNullOrWhiteSpace(fromDb))
                            resultName = fromDb;
                    }
                }
            }

            return resultName;
        }

        /// <summary>
        /// Список исполнителей (smallName) внутри отдела. 
        /// Если divisionId=0, по условию больше не блокируем, возвращаем всех.
        /// </summary>
        public async Task<List<string>> GetExecutorsByDivisionId(int divisionId)
        {
            // При желании можно сделать отдельный кэш под &laquo;divisionId=0 => все&raquo;, 
            // но у вас может быть большая база пользователей. 
            // Для примера условно вернём всех при 0.
            if (divisionId == 0)
            {
                // Возвращаем всех исполнителей (Isvalid=1).
                var allUsers = await _context.Users
                    .Where(u => u.Isvalid == true)
                    .Select(u => u.SmallName)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();
                return allUsers!;
            }

            string cacheKey = $"Executors_{divisionId}";
            if (!_cache.TryGetValue(cacheKey, out List<string?> executors))
            {
                executors = await _context.Users
                    .Where(u => u.IdDivision == divisionId && u.Isvalid == true)
                    .Select(u => u.SmallName)
                    .OrderBy(u => u)
                    .ToListAsync();

                _cache.Set(cacheKey, executors, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                });
            }
            // Преобразуем null в String.Empty не обязательно, если уверены что SmallName не null
            return executors!.Where(x => x != null).Select(x => x!).ToList();
        }

        /// <summary>
        /// Список принимающих (smallName). Если divisionId=0 - возвращаем всех (как в Razor примере).
        /// </summary>
        public async Task<List<string>> GetApproversByDivisionId(int divisionId)
        {
            if (divisionId == 0)
            {
                // Возвращаем всех
                var allUsers = await _context.Users
                    .Where(u => u.Isvalid == true)
                    .Select(u => u.SmallName)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();
                return allUsers!;
            }

            string cacheKey = $"Approvers_{divisionId}";
            if (!_cache.TryGetValue(cacheKey, out List<string?> approvers))
            {
                // В своём SQL/EF запросе можно отфильтровать или нет. 
                // Для примера возвращаем всех valid-пользователей отдела.
                approvers = await _context.Users
                    .Where(u => u.IdDivision == divisionId && u.Isvalid == true)
                    .Select(u => u.SmallName)
                    .OrderBy(u => u)
                    .ToListAsync();

                _cache.Set(cacheKey, approvers, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                });
            }
            return approvers!.Where(x => x != null).Select(x => x!).ToList();
        }

        /// <summary>
        /// Очистить кэш (для указанного divisionId).
        /// </summary>
        public void ClearCache(int divisionId)
        {
            string cacheKey = $"AllWorkItems_div{divisionId}";
            _cache.Remove(cacheKey);

            string exKey = $"Executors_{divisionId}";
            _cache.Remove(exKey);

            string apprKey = $"Approvers_{divisionId}";
            _cache.Remove(apprKey);
        }
    }
}