using Microsoft.AspNetCore.Mvc;
using Monitoring.Application.DTO;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Сервис для работы с WorkItem
    /// </summary>
    public interface IWorkItemAppService
    {
        /// <summary>
        /// Получить список работ (упрощённый пример)
        /// </summary>
        Task<List<WorkItemDto>> GetWorkItemsByDivisionAsync(int divisionId);

        /// <summary>
        /// Получить список работ по фильтрам
        /// </summary>
        Task<List<WorkItemDto>> GetFilteredWorkItemsAsync(
            int divisionId,
            DateOnly? startDate,
            DateOnly? endDate,
            string? executor,
            string? approver,
            string? search
        );

        /// <summary>
        /// Возвращает список исполнителей (smallName) для конкретного divisionId
        /// </summary>
        Task<List<string>> GetExecutorsByDivisionId(int divisionId);

        /// <summary>
        /// Возвращает список "принимающих" (approvers) для конкретного divisionId
        /// (может быть логика по роли, пока сделаем простой вариант)
        /// </summary>
        Task<List<string>> GetApproversByDivisionId(int divisionId);
        void ClearCache(int divisionId);
    }
}