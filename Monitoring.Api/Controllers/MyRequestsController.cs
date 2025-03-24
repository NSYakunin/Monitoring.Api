using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Monitoring.Infrastructure.Services;

namespace Monitoring.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MyRequestsController : ControllerBase
    {
        private readonly IWorkRequestService _workRequestService;
        private readonly IWorkItemAppService _workItemAppService;
        private readonly IUserSettingsService _userSettingsService;
        private readonly ILoginService _loginService;

        public MyRequestsController(
            IWorkRequestService workRequestService,
            IWorkItemAppService workItemAppService,
            IUserSettingsService userSettingsService,
            ILoginService loginService)
        {
            _workRequestService = workRequestService;
            _workItemAppService = workItemAppService;
            _userSettingsService = userSettingsService;
            _loginService = loginService;
        }

        /// <summary>
        /// GET /api/MyRequests
        /// Возвращает все Pending-заявки, где Receiver == текущий пользователь (из JWT), 
        /// при условии, что у него есть право закрывать работы (как в Razor).
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<WorkRequest>>> GetMyRequests()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                return Forbid("Нет userName");

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Forbid("Нет userId");
            int userId = int.Parse(userIdClaim);

            bool canClose = await _userSettingsService.HasAccessToCloseWorkAsync(userId);
            if (!canClose)
                return Forbid("У вас нет права закрывать работы");

            var pending = await _workRequestService.GetPendingRequestsByReceiverAsync(userName);
            return Ok(pending);
        }

        /// <summary>
        /// POST /api/MyRequests/Create
        /// Создание новой заявки.
        /// </summary>
        [HttpPost("Create")]
        public async Task<ActionResult> Create([FromBody] CreateRequestDto dto)
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return Forbid("Нет userName");

                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                    return Forbid("Нет userId");
                int userId = int.Parse(userIdClaim);

                var divisions = await _userSettingsService.GetUserAllowedDivisionsAsync(userId);
                if (divisions.Count == 0)
                {
                    var homeDivClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;
                    if (!string.IsNullOrEmpty(homeDivClaim))
                    {
                        divisions.Add(int.Parse(homeDivClaim));
                    }
                }

                var allWorkItems = await _workItemAppService.GetWorkItemsByDivisionAsync(divisions);
                var witem = allWorkItems.FirstOrDefault(x => x.DocumentNumber == dto.DocumentNumber);
                if (witem == null)
                {
                    return BadRequest(new { success = false, message = "WorkItem не найден" });
                }

                var execs = witem.Executor
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim())
                    .ToList();

                if (!execs.Contains(userName))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Вы не являетесь исполнителем для данной работы."
                    });
                }

                var newRequest = new WorkRequest
                {
                    WorkDocumentNumber = witem.DocumentNumber,
                    DocumentName = witem.DocumentName,
                    WorkName = witem.WorkName,
                    RequestType = dto.RequestType,
                    Sender = userName,
                    Receiver = dto.Receiver,
                    RequestDate = DateTime.Now,
                    IsDone = false,
                    Note = dto.Note,
                    ProposedDate = dto.ProposedDate,
                    Status = "Pending",
                    Executor = witem.Executor,
                    Controller = witem.Controller,
                    PlanDate = witem.PlanDate,
                    Korrect1 = witem.Korrect1,
                    Korrect2 = witem.Korrect2,
                    Korrect3 = witem.Korrect3
                };

                int newId = await _workRequestService.CreateRequestAsync(newRequest);
                return Ok(new { success = true, requestId = newId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/MyRequests/Update
        /// Обновление заявки (RequestType, Receiver, ProposedDate, Note), пока она Pending.
        /// </summary>
        [HttpPost("Update")]
        public async Task<ActionResult> Update([FromBody] UpdateRequestDto dto)
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return Forbid("Нет userName");

                var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(dto.DocumentNumber);
                var req = requests.FirstOrDefault(r => r.Id == dto.Id);
                if (req == null)
                    return BadRequest(new { success = false, message = "Заявка не найдена" });

                if (!req.Sender.Equals(userName, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = "Вы не автор заявки" });
                }

                if (req.Status != "Pending")
                {
                    return BadRequest(new { success = false, message = "Заявка уже обработана" });
                }

                req.RequestType = dto.RequestType;
                req.Receiver = dto.Receiver;
                req.ProposedDate = dto.ProposedDate;
                req.Note = dto.Note;

                await _workRequestService.UpdateRequestAsync(req);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/MyRequests/Delete
        /// Удаление заявки (пока она Pending).
        /// </summary>
        [HttpPost("Delete")]
        public async Task<ActionResult> Delete([FromBody] DeleteRequestDto dto)
        {
            try
            {
                var userName = User.Identity?.Name;
                if (string.IsNullOrEmpty(userName))
                    return Forbid("Нет userName");

                var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(dto.DocumentNumber);
                var req = requests.FirstOrDefault(r => r.Id == dto.RequestId);
                if (req == null)
                    return BadRequest(new { success = false, message = "Заявка не найдена" });

                if (!req.Sender.Equals(userName, StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { success = false, message = "Вы не автор заявки" });

                if (req.Status != "Pending")
                    return BadRequest(new { success = false, message = "Заявка уже обработана" });

                await _workRequestService.DeleteRequestAsync(req.Id);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/MyRequests/SetRequestStatus
        /// Принять/Отклонить заявку.
        /// </summary>
        [HttpPost("SetRequestStatus")]
        public async Task<ActionResult> SetRequestStatus([FromBody] StatusChangeDto dto)
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                return Forbid("Нет userName");

            try
            {
                var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(dto.DocumentNumber);
                var req = requests.FirstOrDefault(r => r.Id == dto.RequestId);
                if (req == null)
                    return BadRequest(new { success = false, message = "Заявка не найдена" });

                if (!req.Receiver.Equals(userName, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = "У вас нет прав на изменение этой заявки" });
                }

                if (dto.NewStatus != "Accepted" && dto.NewStatus != "Declined")
                    return BadRequest(new { success = false, message = "Некорректный статус" });

                await _workRequestService.SetRequestStatusAsync(dto.RequestId, dto.NewStatus);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}