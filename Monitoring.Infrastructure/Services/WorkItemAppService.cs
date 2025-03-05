using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory; // Для кэша
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Monitoring.Domain.Entities;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;

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
        private readonly IUserSettingsService _userSettingsService;
        private readonly string _connectionString;


        public WorkItemAppService(MyDbContext context, IMemoryCache cache, IUserSettingsService userSettingsService, IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string not found");
            _context = context;
            _cache = cache;
            _userSettingsService = userSettingsService;

        }

        /// <summary>
        /// Получить все работы для конкретного подразделения (с учётом того,
        /// что user.IdDivision = divisionId).
        /// Результат кэшируем.
        /// </summary>
        public async Task<List<WorkItemDto>> GetWorkItemsByDivisionAsync(List<int> divisionIds)
        {
            // Если список пустой, возвращаем пустой список (или можно выбросить исключение)
            if (divisionIds == null || divisionIds.Count == 0)
                return new List<WorkItemDto>();

            // Чтобы кэшировать по комбинации подразделений, упорядочим ID 
            // и сформируем строку-ключ: "AllWorkItems_10_12_17"
            var sortedDivs = divisionIds.OrderBy(x => x).ToList();
            string divisionsKeyPart = string.Join("_", sortedDivs);
            string cacheKey = $"AllWorkItems_{divisionsKeyPart}";

            // Пытаемся взять из кэша
            if (!_cache.TryGetValue(cacheKey, out List<WorkItemDto> workItems))
            {
                workItems = new List<WorkItemDto>();

                // Динамическое формирование IN (...)
                var paramNames = new List<string>();
                for (int i = 0; i < sortedDivs.Count; i++)
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
                        -- Имя конкретного пользователя-исполнителя
                        U2.smallName AS Executor,

                        (SELECT smallName 
                         FROM Users 
                         WHERE idUser = wucontr.idUser
                        ) AS Controller,

                        (SELECT smallName 
                         FROM Users 
                         WHERE idUser = wuc.idUser
                        ) AS Approver,

                        w.DatePlan,
                        wu.DateKorrect1,
                        wu.DateKorrect2,
                        wu.DateKorrect3,
                        w.DateFact

                    FROM WorkUser wu
                        INNER JOIN Works w 
                            ON wu.idWork = w.id
                        INNER JOIN Documents d 
                            ON w.idDocuments = d.id
                        LEFT JOIN WorkUserCheck wuc 
                            ON wuc.idWork = w.id
                        LEFT JOIN WorkUserControl wucontr 
                            ON wucontr.idWork = w.id
                        INNER JOIN TypeDocs td 
                            ON td.id = d.idTypeDoc
                        INNER JOIN Users u 
                            ON wu.idUser = u.idUser

                        -- Дополнительные JOIN-ы (EXECUTOR):
                        INNER JOIN WorkUser       wu2   ON wu2.idWork = w.id
                        INNER JOIN Users          U2    ON U2.idUser = wu2.idUser
                        INNER JOIN WorkUserCheck  wuc2  ON wuc2.idWork = w.id

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
                    // Добавляем параметры
                    for (int i = 0; i < sortedDivs.Count; i++)
                    {
                        cmd.Parameters.AddWithValue(paramNames[i], sortedDivs[i]);
                    }

                    await conn.OpenAsync();

                    // Используем словарь для "агрегации" записей:
                    var dict = new Dictionary<string, WorkItemDto>();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Считываем поля
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

                            // Формируем ключ для агрегации (без executor/controller),
                            // чтобы одинаковые записи объединять:
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
                                // Если запись уже есть, "добавляем" исполнителей/контроллеров
                                var existing = dict[key];

                                // Агрегация исполнителей (Executor)
                                if (!string.IsNullOrWhiteSpace(executor))
                                {
                                    var execList = existing.Executor
                                        .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                        .Select(x => x.Trim())
                                        .ToList();

                                    if (!execList.Contains(executor))
                                    {
                                        execList.Add(executor);
                                        existing.Executor = string.Join(", ", execList);
                                    }
                                }

                                // Агрегация контролирующих (Controller)
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

                    // Преобразуем словарь в список
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
        /// Фильтруем полученный список по датам / исполнителю / принимающему / поиску.
        /// </summary>
        public async Task<List<WorkItemDto>> GetFilteredWorkItemsAsync(
            int divisionId,
            DateTime? startDate,
            DateTime? endDate,
            string? executor,
            string? approver,
            string? search,
            int userIdClaim
        )
        {
            var all = await GetWorkItemsByDivisionAsync(_userSettingsService.GetUserAllowedDivisionsAsync(userIdClaim).Result);
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