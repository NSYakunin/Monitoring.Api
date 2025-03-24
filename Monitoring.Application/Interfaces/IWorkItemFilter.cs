using Monitoring.Application.DTO;
using System;
using System.Collections.Generic;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Интерфейс для логики фильтрации WorkItemDto по дате, исполнителю, принимающему, поиску и т.д.
    /// </summary>
    public interface IWorkItemFilter
    {
        List<WorkItemDto> ApplyFilters(
            List<WorkItemDto> source,
            DateTime? startDate,
            DateTime? endDate,
            string? executor,
            string? approver,
            string? search
        );
    }
}