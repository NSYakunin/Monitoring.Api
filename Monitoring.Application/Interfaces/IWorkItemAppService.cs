using Monitoring.Application.DTO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Monitoring.Application.Interfaces
{
    public interface IWorkItemAppService
    {
        // Было:
        // Task<List<WorkItemDto>> GetFilteredWorkItemsAsync(...);

        // Стало: для пагинации
        Task<PagedWorkItemsDto> GetFilteredWorkItemsAsync(
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
        );

        // И метод, который просто возвращает полный список, без paging:
        Task<List<WorkItemDto>> GetAllFilteredWithoutPaging(
            int divisionId,
            DateTime? startDate,
            DateTime? endDate,
            string? executor,
            string? approver,
            string? search,
            int userIdClaim,
            string? currentUserName
        );

        Task<List<WorkItemDto>> GetWorkItemsByDivisionAsync(List<int> divisionIds);
        Task<string> GetDevNameAsync(int divisionId);
        Task<List<string>> GetExecutorsByDivisionId(int divisionId);
        Task<List<string>> GetApproversByDivisionId(int divisionId);
        void ClearCache(int divisionId);
    }
}