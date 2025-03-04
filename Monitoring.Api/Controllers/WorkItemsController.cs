using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using System.Security.Claims;

namespace Monitoring.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Требуем наличие JWT (Bearer-token) для всех методов (кроме AllowAnonymous)
    public class WorkItemsController : ControllerBase
    {
        private readonly IWorkItemAppService _workItemAppService;
        private readonly IUserSettingsService _userSettingsService; // для примера "GetAllowedDivisions"

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
        /// Параметры: ?startDate=2025-01-01&endDate=2025-02-01&executor=...&approver=...&search=...
        /// divisionId берем из Claims (JWT).
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
            try
            {
                // Считываем divisionId из JWT
                var userDivClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;
                if (string.IsNullOrEmpty(userDivClaim))
                {
                    return Forbid("У вас нет 'divisionId' в токене.");
                }
                int divisionId = int.Parse(userDivClaim);

                // Логика дат по умолчанию (2014-01-01 ... конец текущего месяца)
                if (!startDate.HasValue)
                    startDate = new DateOnly(2014, 1, 1);

                if (!endDate.HasValue)
                {
                    var now = DateTime.Now;
                    // последний день текущего месяца
                    endDate = new DateOnly(now.Year, now.Month, 1)
                        .AddMonths(1)
                        .AddDays(-1);
                }

                var items = await _workItemAppService.GetFilteredWorkItemsAsync(
                    divisionId,
                    startDate,
                    endDate,
                    executor,
                    approver,
                    search
                );

                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Ошибка сервера: " + ex.Message);
            }
        }

        /// <summary>
        /// GET /api/WorkItems/AllowedDivisions
        /// Возвращает "AllowedDivisions" для текущего пользователя, исходя из userId в токене
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

                // При желании можем добавить в этот список и "свой" divisionId, если его там нет
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
    }
}