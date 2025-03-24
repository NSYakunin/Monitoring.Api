using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Сервис фильтрации WorkItemDto: по исполнителю, принимающему, 
    /// строковому поиску и интервалу дат.
    /// </summary>
    public class WorkItemFilter : IWorkItemFilter
    {
        public List<WorkItemDto> ApplyFilters(
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
                query = query.Where(x =>
                    x.Executor != null &&
                    x.Executor.Contains(executor, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(approver))
            {
                query = query.Where(x =>
                    (x.Approver ?? "")
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
                // (Korrect3 ?? Korrect2 ?? Korrect1 ?? PlanDate) <= endDate
                query = query.Where(x => (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) <= endDate.Value);
            }

            if (startDate.HasValue)
            {
                // (Korrect3 ?? Korrect2 ?? Korrect1 ?? PlanDate) >= startDate
                query = query.Where(x => (x.Korrect3 ?? x.Korrect2 ?? x.Korrect1 ?? x.PlanDate) >= startDate.Value);
            }

            return query.ToList();
        }
    }
}