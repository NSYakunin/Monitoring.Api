using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Data;
using Monitoring.Infrastructure.Data.ScaffoldModels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System;

namespace Monitoring.Infrastructure.Repositories
{
    /// <summary>
    /// Репозиторий для работы с WorkItem'ами: 
    /// - получение всех работ из БД (SQL),
    /// - кэширование,
    /// - агрегация дублей,
    /// - получение названий подразделений и списков пользователей.
    /// </summary>
    public class WorkItemRepository : IWorkItemRepository
    {
        private readonly MyDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly string _connectionString;

        public WorkItemRepository(
            MyDbContext context,
            IMemoryCache cache,
            IConfiguration configuration
        )
        {
            _context = context;
            _cache = cache;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string not found");
        }

        /// <summary>
        /// Получить все WorkItemDto для списка divisionIds,
        /// причём для каждого divisionId данные берутся из кэша или БД,
        /// затем объединяются в единый список, и дубли агрегируются
        /// (если одна и та же работа, объединяем исполнителей и контролирующих).
        /// </summary>
        public async Task<List<WorkItemDto>> GetWorkItemsForDivisionsAsync(List<int> divisionIds)
        {
            if (divisionIds == null || divisionIds.Count == 0)
                return new List<WorkItemDto>();

            var allItems = new List<WorkItemDto>();

            // Для каждого отдела тянем данные (с кэшом)
            foreach (var divId in divisionIds.Distinct())
            {
                var itemsForDiv = await GetAllWorkItemsForSingleDivision(divId);
                allItems.AddRange(itemsForDiv);
            }

            // Агрегируем дубликаты:
            // ключ: набор значимых полей (DocumentName, WorkName, Approver, PlanDate ...).
            var dict = new Dictionary<string, WorkItemDto>();

            foreach (var w in allItems)
            {
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
                    // Если уже есть такая запись, нужно объединить исполнителей и контролирующих
                    var existing = dict[key];

                    // Агрегируем исполнителей
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
        /// Получить все работы для ОДНОГО отдела (используя кэш).
        /// </summary>
        private async Task<List<WorkItemDto>> GetAllWorkItemsForSingleDivision(int divisionId)
        {
            string cacheKey = $"AllWorkItems_div{divisionId}";

            if (_cache.TryGetValue(cacheKey, out List<WorkItemDto> cached))
                return cached;

            var result = new List<WorkItemDto>();

            // SQL-запрос (аналог, что был в исходном коде)
            using (var conn = new SqlConnection(_connectionString))
            {
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

            // Кладём в кэш
            var cacheOpts = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };
            _cache.Set(cacheKey, result, cacheOpts);

            return result;
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

        /// <summary>
        /// Получить название отдела (smallNameDivision) из таблицы Divisions.
        /// </summary>
        public async Task<string> GetDivisionNameAsync(int divisionId)
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
        /// Если divisionId=0, то вернуть всех исполнителей (Isvalid=1).
        /// </summary>
        public async Task<List<string>> GetExecutorsByDivisionIdAsync(int divisionId)
        {
            if (divisionId == 0)
            {
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

            return executors!.Where(x => x != null).Select(x => x!).ToList();
        }

        /// <summary>
        /// Список принимающих (smallName). 
        /// Если divisionId=0, возвращаем всех valid-пользователей.
        /// </summary>
        public async Task<List<string>> GetApproversByDivisionIdAsync(int divisionId)
        {
            if (divisionId == 0)
            {
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
    }
}