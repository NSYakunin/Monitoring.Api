using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Monitoring.Infrastructure.Services;

namespace Monitoring.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Требуем наличие JWT (Bearer-token) для всех методов (кроме AllowAnonymous)
    public class WorkItemsController : ControllerBase
    {
        private readonly IWorkItemAppService _workItemAppService;
        private readonly IUserSettingsService _userSettingsService;
        private readonly IWorkRequestService _workRequestService;

        public WorkItemsController(
            IWorkItemAppService workItemAppService,
            IUserSettingsService userSettingsService,
            IWorkRequestService workRequestService
        )
        {
            _workItemAppService = workItemAppService;
            _userSettingsService = userSettingsService;
            _workRequestService = workRequestService;
        }

        [HttpGet]
        public async Task<ActionResult<List<WorkItemDto>>> GetFilteredWorkItems(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? executor,
            [FromQuery] string? approver,
            [FromQuery] string? search,
            [FromQuery] int? divisionId
        )
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Forbid("Нет userId");

            int userId = int.Parse(userIdClaim);

            // Ищем userName
            var userName = User.Identity?.Name; // частая практика
            if (string.IsNullOrEmpty(userName))
                return Forbid("Нет userName");

            // Родной отдел (если divisionId не передали)
            var divIdClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;
            if (string.IsNullOrEmpty(divIdClaim))
                return Forbid("Нет divisionId в токене");

            int homeDivId = int.Parse(divIdClaim);

            int realDivId = divisionId ?? homeDivId;

            if (!startDate.HasValue)
                startDate = new DateTime(2014, 1, 1);

            if (!endDate.HasValue)
            {
                var now = DateTime.Now;
                endDate = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);
            }

            // 1) Получаем отфильтрованные WorkItems
            var workItems = await _workItemAppService.GetFilteredWorkItemsAsync(
                realDivId, startDate, endDate, executor, approver, search, userId
            );

            // 2) "Подсвечиваем" строки, как было в Razor
            //    Для каждой строки смотрим, есть ли "Pending"-заявка от текущего пользователя
            foreach (var item in workItems)
            {
                // Ищем все заявки по DocumentNumber
                var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(item.DocumentNumber);

                // PENDING от текущего пользователя (Sender == userName)
                var pendingFromMe = requests.FirstOrDefault(r =>
                    r.Status == "Pending"
                    && !r.IsDone
                    && r.Sender.Equals(userName, StringComparison.OrdinalIgnoreCase)
                );

                if (pendingFromMe != null)
                {
                    // ставим CSS-класс
                    if (pendingFromMe.RequestType == "факт")
                        item.HighlightCssClass = "table-info";
                    else if (pendingFromMe.RequestType.StartsWith("корр"))
                        item.HighlightCssClass = "table-warning";

                    // заполняем дополнительные поля
                    item.UserPendingRequestId = pendingFromMe.Id;
                    item.UserPendingRequestType = pendingFromMe.RequestType;
                    item.UserPendingProposedDate = pendingFromMe.ProposedDate;
                    item.UserPendingRequestNote = pendingFromMe.Note;
                    item.UserPendingReceiver = pendingFromMe.Receiver;
                }
            }

            return Ok(workItems);
        }

        [HttpGet("AllowedDivisions")]
        public async Task<ActionResult<List<int>>> GetAllowedDivisions()
        {
            try
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null) return Forbid("Нет userId");
                int userId = int.Parse(userIdClaim);

                var divisions = await _userSettingsService.GetUserAllowedDivisionsAsync(userId);

                var divIdClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;
                if (!string.IsNullOrEmpty(divIdClaim))
                {
                    int homeDiv = int.Parse(divIdClaim);
                    if (!divisions.Contains(homeDiv))
                        divisions.Add(homeDiv);
                }

                return Ok(divisions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Ошибка сервера: " + ex.Message);
            }
        }

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
        /// НОВЫЙ метод: Возвращает строку с названием отдела.
        /// GET /api/WorkItems/DivisionName?divisionId=XX
        /// </summary>
        [HttpGet("DivisionName")]
        public async Task<ActionResult<string>> GetDivisionName([FromQuery] int divisionId)
        {
            try
            {
                var name = await _workItemAppService.GetDevNameAsync(divisionId);
                return Ok(name);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Ошибка: " + ex.Message);
            }
        }

        [HttpPost("ClearCache")]
        public ActionResult ClearCache([FromQuery] int divisionId)
        {
            _workItemAppService.ClearCache(divisionId);
            return Ok(new { success = true });
        }
    }
}