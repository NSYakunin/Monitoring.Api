using Monitoring.Application.DTO;

namespace Monitoring.Application.Interfaces
{
    public interface IWorkItemAppService
    {
        /// <summary>
        /// Получить работы для конкретного отдела (в базовом виде без фильтра).
        /// </summary>
        Task<List<WorkItemDto>> GetWorkItemsByDivisionAsync(int divisionId);

        /// <summary>
        /// Более продвинутый метод, который учитывает фильтры:
        /// startDate, endDate, executor, approver, search.
        /// </summary>
        Task<List<WorkItemDto>> GetFilteredWorkItemsAsync(
            int divisionId,
            DateOnly? startDate,
            DateOnly? endDate,
            string? executor,
            string? approver,
            string? search
        );

        // При необходимости – другие методы
    }
}