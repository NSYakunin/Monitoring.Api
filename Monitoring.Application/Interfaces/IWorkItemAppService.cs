using Monitoring.Application.DTO;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Сервис (Application Service) для работы с WorkItem'ами.
    /// </summary>
    public interface IWorkItemAppService
    {
        /// <summary>
        /// Получить список работ (WorkItems) по ID подразделения.
        /// </summary>
        Task<List<WorkItemDto>> GetWorkItemsByDivisionAsync(int divisionId);
    }
}   
