using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace Monitoring.Api.Controllers
{
    /// <summary>
    /// Контроллер для получения отчёта по исполнению.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PerformanceController : ControllerBase
    {
        private readonly IPerformanceService _performanceService;

        public PerformanceController(IPerformanceService performanceService)
        {
            _performanceService = performanceService;
        }

        /// <summary>
        /// GET-запрос на получение списка подразделений с планом/фактом/процентом 
        /// за указанный период.
        /// Если даты не указаны, берём с 1-го числа текущего месяца по сегодняшний день.
        /// </summary>
        [HttpGet]
        public ActionResult<List<PerformanceDto>> GetPerformance(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var end = endDate ?? DateTime.Now.Date;

            var results = _performanceService.GetPerformanceData(start, end);
            return Ok(results);
        }
    }
}