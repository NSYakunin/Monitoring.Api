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

namespace Monitoring.Infrastructure.Services
{
    public class WorkItemAppService : IWorkItemAppService
    {
        private readonly MyDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IUserSettingsService _userSettingsService;
        private readonly IWorkRequestService _workRequestService; // Чтобы подсвечивать
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

        // --- НОВЫЙ метод: Пагинированная версия ---
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
            // Если divisionId == 0, значит нужно взять все доступные отделы:
            List<int> divisionsToLoad;
            if (divisionId == 0)
            {
                var userDivs = await _userSettingsService.GetUserAllowedDivisionsAsync(userIdClaim);
                // Если у пользователя совсем нет прописанных — берём родной отдел
                if (userDivs.Count == 0)
                {
                    // На случай, если в токене есть
                    // (но это логика уже на ваше усмотрение)
                    divisionsToLoad = new List<int>();
                }
                else
                {
                    divisionsToLoad = userDivs;
                }
            }
            else
            {
                divisionsToLoad = new List<int> { divisionId };
            }

            // Если нет отделов в списке — вернём пустоту
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

            // Получаем все работы
            var all = await GetWorkItemsByDivisionAsync(divisionsToLoad);

            // Фильтруем
            var filtered = ApplyFilters(all, startDate, endDate, executor, approver, search);

            //// Подсветим, если нужно
            if (!string.IsNullOrEmpty(currentUserName))
            {
                await HighlightRows(filtered, currentUserName);
            }

            // Считаем пагинацию
            int totalCount = filtered.Count;
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            if (pageNumber < 1) pageNumber = 1;
            if (pageNumber > totalPages && totalPages > 0) pageNumber = totalPages;

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

        // --- НОВЫЙ метод: без пагинации (для экспорта) ---
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
            // Если divisionId=0 => все отделы
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

            if (divisionsToLoad.Count == 0) return new List<WorkItemDto>();

            var all = await GetWorkItemsByDivisionAsync(divisionsToLoad);
            var filtered = ApplyFilters(all, startDate, endDate, executor, approver, search);

            if (!string.IsNullOrEmpty(currentUserName))
            {
                await HighlightRows(filtered, currentUserName);
            }

            return filtered;
        }

        // ---------------------------------------------------
        // Вспомогательная функция: сама фильтрация (чтоб не дублировать)
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
                query = query.Where(x => (x.Approver ?? "").Contains(approver, StringComparison.OrdinalIgnoreCase));
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


        // Для удобства, добавим вспомогательный метод преобразования:
        // (Чтобы при экспорте легко перегнать Dto в сущность для ReportGenerator)

        /// <summary>
        /// Получить все работы (WorkItemDto) по списку подразделений (divisionIds).
        /// </summary>
        public async Task<List<WorkItemDto>> GetWorkItemsByDivisionAsync(List<int> divisionIds)
        {
            if (divisionIds == null || divisionIds.Count == 0)
                return new List<WorkItemDto>();

            // Формируем ключ кэша
            var sorted = divisionIds.OrderBy(x => x).ToList();
            string keyPart = string.Join("_", sorted);
            string cacheKey = $"AllWorkItems_{keyPart}";

            if (!_cache.TryGetValue(cacheKey, out List<WorkItemDto> workItems))
            {
                workItems = new List<WorkItemDto>();

                // Генерируем IN (...)
                var paramNames = new List<string>();
                for (int i = 0; i < sorted.Count; i++)
                {
                    paramNames.Add($"@div{i}");
                }
                string inClause = string.Join(", ", paramNames);

                string query = $@"
                    SELECT 
                        d.Number,
                        wu.idWork,
                        td.Name + ' ' + d.Name AS DocumentName,
                        w.Name AS WorkName,
                        U2.smallName AS Executor,
                        (SELECT smallName FROM Users WHERE idUser = wucontr.idUser) AS Controller,
                        (SELECT smallName FROM Users WHERE idUser = wuc.idUser)     AS Approver,
                        w.DatePlan,
                        wu.DateKorrect1,
                        wu.DateKorrect2,
                        wu.DateKorrect3,
                        w.DateFact
                    FROM WorkUser wu
                        INNER JOIN Works w ON wu.idWork = w.id
                        INNER JOIN Documents d ON w.idDocuments = d.id
                        LEFT JOIN WorkUserCheck wuc ON wuc.idWork = w.id
                        LEFT JOIN WorkUserControl wucontr ON wucontr.idWork = w.id
                        INNER JOIN TypeDocs td ON td.id = d.idTypeDoc
                        INNER JOIN Users u ON wu.idUser = u.idUser
                        -- Доп JOIN на Executor:
                        INNER JOIN WorkUser wu2 ON wu2.idWork = w.id
                        INNER JOIN Users    U2  ON U2.idUser = wu2.idUser
                        INNER JOIN WorkUserCheck wuc2 ON wuc2.idWork = w.id
                    WHERE
                        wu.dateFact IS NULL
                        AND wu.idUser IN (
                            SELECT idUser 
                            FROM Users 
                            WHERE idDivision IN ({inClause})
                        );
                ";

                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(query, conn))
                {
                    // Подставляем параметры
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        cmd.Parameters.AddWithValue(paramNames[i], sorted[i]);
                    }

                    await conn.OpenAsync();

                    var dict = new Dictionary<string, WorkItemDto>();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string idWork = reader["idWork"]?.ToString();
                            string docNumber = reader["Number"]?.ToString() + "/" + reader["idWork"]?.ToString();
                            string docName = reader["DocumentName"]?.ToString();
                            string workName = reader["WorkName"]?.ToString();
                            string executor = reader["Executor"]?.ToString();
                            string controller = reader["Controller"]?.ToString();
                            string approver = reader["Approver"]?.ToString();
                            DateTime? planDate = reader["DatePlan"] as DateTime?;
                            DateTime? kor1 = reader["DateKorrect1"] as DateTime?;
                            DateTime? kor2 = reader["DateKorrect2"] as DateTime?;
                            DateTime? kor3 = reader["DateKorrect3"] as DateTime?;
                            DateTime? factDate = reader["DateFact"] as DateTime?;

                            // Ключ для агрегации
                            string key = $"{docName}|{workName}|{approver}|{planDate}|{kor1}|{kor2}|{kor3}|{factDate}|{idWork}";

                            if (!dict.ContainsKey(key))
                            {
                                dict[key] = new WorkItemDto
                                {
                                    DocumentNumber = docNumber,
                                    DocumentName = docName ?? "",
                                    WorkName = workName ?? "",
                                    Executor = executor ?? "",
                                    Controller = controller ?? "",
                                    Approver = approver ?? "",
                                    PlanDate = planDate,
                                    Korrect1 = kor1,
                                    Korrect2 = kor2,
                                    Korrect3 = kor3,
                                    FactDate = factDate
                                };
                            }
                            else
                            {
                                // Если запись уже есть, дополняем исполнителей
                                var existing = dict[key];

                                // Агрегация исполнителей (Executor)
                                if (!string.IsNullOrWhiteSpace(executor))
                                {
                                    var arr = existing.Executor
                                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(x => x.Trim())
                                        .ToList();
                                    if (!arr.Contains(executor))
                                    {
                                        arr.Add(executor);
                                        existing.Executor = string.Join(", ", arr);
                                    }
                                }

                                // Аналогично для контроллера (Controller)
                                if (!string.IsNullOrWhiteSpace(controller))
                                {
                                    var ctrlList = existing.Controller
                                        .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                        .Select(x => x.Trim())
                                        .ToList();

                                    if (!ctrlList.Contains(controller))
                                    {
                                        ctrlList.Add(controller);
                                        existing.Controller = string.Join(", ", ctrlList);
                                    }
                                }
                            }
                        }
                    }
                    workItems = dict.Values.ToList();
                }

                // Сохраняем в кэш
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                };
                _cache.Set(cacheKey, workItems, cacheOptions);
            }

            return workItems;
        }

        /// <summary>
        /// Получить "имя" отдела (smallNameDivision).
        /// Если оно пустое, вернём "Отдел #{divisionId}".
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
        /// Получить список исполнителей (smallName) внутри одного отдела, с кэшированием.
        /// </summary>
        public async Task<List<string>> GetExecutorsByDivisionId(int divisionId)
        {
            // Формируем ключ кэша
            string cacheKey = $"Executors_{divisionId}";

            // Пытаемся взять из кэша
            if (!_cache.TryGetValue(cacheKey, out List<string?> executors))
            {
                // Если нет в кэше, грузим из базы
                executors = await _context.Users
                    .Where(u => u.IdDivision == divisionId && u.Isvalid == true)
                    .Select(u => u.SmallName)
                    .OrderBy(u => u)
                    .ToListAsync();

                // Сохраняем в кэш
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                };
                _cache.Set(cacheKey, executors, cacheOptions);
            }

            return executors;
        }

        /// <summary>
        /// Получить список принимающих (smallName) внутри одного отдела, с кэшированием.
        /// </summary>
        public async Task<List<string>> GetApproversByDivisionId(int divisionId)
        {
            // Формируем ключ кэша
            string cacheKey = $"Approvers_{divisionId}";

            // Пытаемся взять из кэша
            if (!_cache.TryGetValue(cacheKey, out List<string?> approvers))
            {
                // Если нет в кэше, грузим из базы
                approvers = await _context.Users
                    .Where(u => u.Isvalid)
                    .Select(u => u.SmallName)
                    .OrderBy(u => u)
                    .ToListAsync();

                // Сохраняем в кэш
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                };
                _cache.Set(cacheKey, approvers, cacheOptions);
            }

            return approvers;
        }

        // ---------------------------------------------------
        // Вспомогательная функция: подсветка (HighlightRows)
        private async Task HighlightRows(List<WorkItemDto> items, string currentUserName)
        {
            // Для каждой строки ищем заявки (Pending) от текущего пользователя
            // (Нужно обратиться к _workRequestService.GetRequestsByDocumentNumberAsync)
            // Чтобы не делать слишком много запросов, можно сделать группировку и т.д.
            // Но для наглядности используем прямой подход.

            // Группируем по DocumentNumber, потом одним запросом получим все заявки сразу,
            // а затем сопоставим. (Оптимизация - на ваше усмотрение)
            var docNumbers = items.Select(x => x.DocumentNumber).Distinct().ToList();
            // Для упрощения сейчас - просто в цикле (но тогда будет n запросов).
            // Если надо оптимизировать - сами сделайте. 

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
        /// Очистка кэша для указанного отдела. Теперь также удаляем ключи исполнителей и принимающих.
        /// </summary>
        public void ClearCache(int divisionId)
        {
            // Удаляем кэш со всеми работами
            string singleKey = $"AllWorkItems_{divisionId}";
            _cache.Remove(singleKey);

            // Удаляем кэш исполнителей
            string exKey = $"Executors_{divisionId}";
            _cache.Remove(exKey);

            // Удаляем кэш принимающих
            string apprKey = $"Approvers_{divisionId}";
            _cache.Remove(apprKey);
        }
    }
}