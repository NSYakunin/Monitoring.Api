using Monitoring.Domain.Entities;
using System;
using System.Collections.Generic;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Контракт (интерфейс) сервиса для получения данных об исполнении.
    /// </summary>
    public interface IPerformanceService
    {
        /// <summary>
        /// Возвращает список данных о выполнении (План, Факт, %).
        /// </summary>
        List<PerformanceDto> GetPerformanceData(DateTime startDate, DateTime endDate);
    }
}