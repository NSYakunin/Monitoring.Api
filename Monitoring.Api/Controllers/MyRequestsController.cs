using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Monitoring.Infrastructure.Services;

namespace Monitoring.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MyRequestsController : ControllerBase
    {
        private readonly IWorkRequestService _workRequestService;
        private readonly IUserSettingsService _userSettingsService;
        private readonly ILoginService _loginService;

        public MyRequestsController(
            IWorkRequestService workRequestService,
            IUserSettingsService userSettingsService,
            ILoginService loginService
        )
        {
            _workRequestService = workRequestService;
            _userSettingsService = userSettingsService;
            _loginService = loginService;
        }

        /// <summary>
        /// GET /api/MyRequests
        /// Возвращает все Pending-заявки, где Receiver == текущий пользователь (из JWT).
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<WorkRequest>>> GetMyRequests()
        {
            // Находим userName из JWT
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName)) return Forbid("Нет userName");

            // Проверяем, есть ли право на закрытие (необязательно, но как в Razor)
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Forbid("Нет userId");
            int userId = int.Parse(userIdClaim);

            var canClose = await _userSettingsService.HasAccessToCloseWorkAsync(userId);
            if (!canClose)
            {
                // Можно вернуть пустой список или 403
                return Forbid("У вас нет права закрывать работы");
            }

            var result = await _workRequestService.GetPendingRequestsByReceiverAsync(userName);
            return Ok(result);
        }

        /// <summary>
        /// POST /api/MyRequests/SetRequestStatus
        /// Установить статус заявки (Accepted/Declined).
        /// </summary>
        [HttpPost("SetRequestStatus")]
        public async Task<ActionResult> SetRequestStatus([FromBody] StatusChangeDto dto)
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                return Forbid("Нет userName");

            // Ищем заявку
            var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(dto.DocumentNumber);
            var req = requests.FirstOrDefault(r => r.Id == dto.RequestId);
            if (req == null)
                return BadRequest(new { success = false, message = "Заявка не найдена" });

            if (req.Receiver != userName)
                return Forbid("Вы не являетесь получателем");

            if (dto.NewStatus != "Accepted" && dto.NewStatus != "Declined")
                return BadRequest(new { success = false, message = "Некорректный статус" });

            await _workRequestService.SetRequestStatusAsync(dto.RequestId, dto.NewStatus);
            return Ok(new { success = true });
        }
    }

    public class StatusChangeDto
    {
        public int RequestId { get; set; }
        public string DocumentNumber { get; set; }
        public string NewStatus { get; set; }
    }
}