using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Monitoring.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Требуем наличие JWT (Bearer-token) для всех методов (кроме AllowAnonymous)
    public class WorkItemsController : ControllerBase
    {
        private readonly IWorkItemAppService _workItemAppService;
        private readonly IUserSettingsService _userSettingsService;

        public WorkItemsController(
            IWorkItemAppService workItemAppService,
            IUserSettingsService userSettingsService
        )
        {
            _workItemAppService = workItemAppService;
            _userSettingsService = userSettingsService;
        }

        /// <summary>
        /// GET /api/WorkItems
        /// Фильтры: ?startDate=...&endDate=...&executor=...&approver=...&search=...
        /// divisionId берём из JWT claim.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<WorkItemDto>>> GetFilteredWorkItems(
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? executor,
            [FromQuery] string? approver,
            [FromQuery] string? search
        )
        {
            var divIdClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;
            if (string.IsNullOrEmpty(divIdClaim))
            {
                return Forbid("Нет divisionId в токене");
            }

            int divisionId = int.Parse(divIdClaim);

            if (!startDate.HasValue)
                startDate = new DateOnly(2014, 1, 1);
            if (!endDate.HasValue)
            {
                var now = DateTime.Now;
                endDate = new DateOnly(now.Year, now.Month, 1)
                    .AddMonths(1)
                    .AddDays(-1);
            }

            var items = await _workItemAppService.GetFilteredWorkItemsAsync(
                divisionId, startDate, endDate, executor, approver, search
            );
            return Ok(items);
        }

        /// <summary>
        /// GET /api/WorkItems/AllowedDivisions
        /// Возвращает список ID отделов, к которым пользователь имеет доступ (включая родной).
        /// </summary>
        [HttpGet("AllowedDivisions")]
        public async Task<ActionResult<List<int>>> GetAllowedDivisions()
        {
            try
            {
                // Достаём userId из Claims
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return Forbid("У вас нет userId в токене (ClaimTypes.NameIdentifier).");
                }
            int userId = int.Parse(userIdClaim);

                var divisions = await _userSettingsService.GetUserAllowedDivisionsAsync(userId);

            // Добавим родной, если нет
            var divIdClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;
            if (!string.IsNullOrEmpty(divIdClaim))
            {
                    int currentDivision = int.Parse(divIdClaim);
                    if (!divisions.Contains(currentDivision))
                    {
                        divisions.Add(currentDivision);
                    }
            }

                return Ok(divisions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Ошибка сервера: " + ex.Message);
            }
        }

        /// <summary>
        /// GET /api/WorkItems/Executors?divisionId=...
        /// Для загрузки списка исполнителей из БД.
        /// </summary>
        [HttpGet("Executors")]
        public async Task<ActionResult<List<string>>> GetExecutors([FromQuery] int divisionId)
        {
            try
            {
            var list = await _workItemAppService.GetExecutorsByDivisionId(divisionId);
            return Ok(list);
        }
            catch (Exception ex)
            {
                return StatusCode(500, "Ошибка сервера: " + ex.Message);
            }
        }

        /// <summary>
        /// GET /api/WorkItems/Approvers?divisionId=...
        /// Для загрузки списка принимающих (пока аналогично исполнителям).
        /// </summary>
        [HttpGet("Approvers")]
        public async Task<ActionResult<List<string>>> GetApprovers([FromQuery] int divisionId)
        {
            try
            {
                var list = await _workItemAppService.GetApproversByDivisionId(divisionId);
                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Ошибка сервера: " + ex.Message);
            }
        }

        /// <summary>
        /// POST /api/WorkItems/ClearCache?divisionId=...
        /// (Если хотите аналог "RefreshCache", как в Razor)
        /// </summary>
        [HttpPost("ClearCache")]
        public ActionResult ClearCache([FromQuery] int divisionId)
        {
            _workItemAppService.ClearCache(divisionId);
            return Ok(new { success = true });
        }
    }
}