using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Сервис прикладного уровня для работы с WorkItem:
    /// отвечает за 
    /// 1) вычисление, какие divisionId нужно загрузить,
    /// 2) вызов репозитория для получения данных,
    /// 3) вызов фильтра, подсветки,
    /// 4) пагинацию.
    /// </summary>
    public class WorkItemAppService : IWorkItemAppService
    {
        private readonly IWorkItemRepository _workItemRepository;
        private readonly IUserSettingsService _userSettingsService;
        private readonly IWorkItemFilter _workItemFilter;
        private readonly IWorkItemHighlighter _workItemHighlighter;

        public WorkItemAppService(
            IWorkItemRepository workItemRepository,
            IUserSettingsService userSettingsService,
            IWorkItemFilter workItemFilter,
            IWorkItemHighlighter workItemHighlighter
        )
        {
            _workItemRepository = workItemRepository;
            _userSettingsService = userSettingsService;
            _workItemFilter = workItemFilter;
            _workItemHighlighter = workItemHighlighter;
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
            // Определяем, какие отделы нужно загружать
            List<int> divisionsToLoad = await DetermineDivisionsAsync(divisionId, userIdClaim);

            if (divisionsToLoad.Count == 0)
            {
                // Если нет вообще доступных отделов — возвращаем пустоту
                return new PagedWorkItemsDto
                {
                    Items = new List<WorkItemDto>(),
                    CurrentPage = pageNumber,
                    PageSize = pageSize,
                    TotalPages = 1,
                    TotalCount = 0
                };
            }

            // Грузим все работы (с кэшом, SQL и агрегацией)
            var all = await _workItemRepository.GetWorkItemsForDivisionsAsync(divisionsToLoad);

            // Фильтрация
            var filtered = _workItemFilter.ApplyFilters(all, startDate, endDate, executor, approver, search);

            // Подсветка (если знаем текущего пользователя)
            if (!string.IsNullOrEmpty(currentUserName))
            {
                await _workItemHighlighter.HighlightRowsAsync(filtered, currentUserName);
            }

            // Пагинация
            int totalCount = filtered.Count;
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            if (pageNumber < 1) pageNumber = 1;
            if (totalPages < 1) totalPages = 1;
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
            List<int> divisionsToLoad = await DetermineDivisionsAsync(divisionId, userIdClaim);
            if (divisionsToLoad.Count == 0)
                return new List<WorkItemDto>();

            var all = await _workItemRepository.GetWorkItemsForDivisionsAsync(divisionsToLoad);
            var filtered = _workItemFilter.ApplyFilters(all, startDate, endDate, executor, approver, search);

            if (!string.IsNullOrEmpty(currentUserName))
            {
                await _workItemHighlighter.HighlightRowsAsync(filtered, currentUserName);
            }

            return filtered;
        }

        /// <summary>
        /// ПОЛНОСТЬЮ (с агрегацией) получить все WorkItemsDto для списка отделов (divisionIds).
        /// Без фильтрации/подсветки — просто вызываем репозиторий.
        /// </summary>
        public async Task<List<WorkItemDto>> GetWorkItemsByDivisionAsync(List<int> divisionIds)
        {
            if (divisionIds == null || divisionIds.Count == 0)
                return new List<WorkItemDto>();

            var allItems = await _workItemRepository.GetWorkItemsForDivisionsAsync(divisionIds);
            return allItems;
        }

        /// <summary>
        /// Очистить кэш (для указанного divisionId).
        /// </summary>
        public void ClearCache(int divisionId)
        {
            _workItemRepository.ClearCache(divisionId);
        }

        /// <summary>
        /// Получить название отдела (smallNameDivision).
        /// </summary>
        public async Task<string> GetDevNameAsync(int divisionId)
        {
            return await _workItemRepository.GetDivisionNameAsync(divisionId);
        }

        /// <summary>
        /// Список исполнителей (smallName) внутри отдела. Если divisionId=0 - вернуть всех.
        /// </summary>
        public async Task<List<string>> GetExecutorsByDivisionId(int divisionId)
        {
            return await _workItemRepository.GetExecutorsByDivisionIdAsync(divisionId);
        }

        /// <summary>
        /// Список принимающих (smallName). Если divisionId=0 - вернуть всех.
        /// </summary>
        public async Task<List<string>> GetApproversByDivisionId(int divisionId)
        {
            return await _workItemRepository.GetApproversByDivisionIdAsync(divisionId);
        }

        /// <summary>
        /// Вспомогательный метод: определить список отделов, 
        /// которые нужно загрузить (учитывая divisionId=0 или конкретный).
        /// </summary>
        private async Task<List<int>> DetermineDivisionsAsync(int divisionId, int userIdClaim)
        {
            if (divisionId == 0)
            {
                var userDivs = await _userSettingsService.GetUserAllowedDivisionsAsync(userIdClaim);
                if (userDivs.Count == 0)
                    return new List<int>(); // пусто, если у пользователя нет настроенных отделов

                return userDivs;
            }
            else
            {
                return new List<int> { divisionId };
            }
        }
    }
}