using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using Monitoring.Application.Services;

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

        // ----------------------------------------------------------------
        //  НОВЫЙ Метод "Export", аналог Razor OnPostAsync() из IndexModel
        // ----------------------------------------------------------------
        [HttpPost("Export")]
        public async Task<IActionResult> Export([FromBody] ExportRequestDto request)
        {
            try
            {
                // 1) Проверяем JWT
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var userName = User.Identity?.Name;
                var divIdClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(divIdClaim))
                {
                    return Forbid("Нет необходимых данных в токене (userId, userName, divisionId).");
                }

                int userId = int.Parse(userIdClaim);
                int homeDivisionId = int.Parse(divIdClaim);

                // 2) Определяем реальный отдел
                int actualDivisionId = request.DivisionId ?? homeDivisionId;

                // 3) Подгружаем список работ с учётом фильтров
                var startDate = request.StartDate ?? new DateTime(2014, 1, 1);
                var endDate = request.EndDate ?? DateTime.Now;

                var workItems = await _workItemAppService.GetFilteredWorkItemsAsync(
                    actualDivisionId,
                    startDate,
                    endDate,
                    request.Executor,
                    request.Approver,
                    request.Search,
                    userId
                );

                // 4) Узнаем "название" подразделения
                string depName = await _workItemAppService.GetDevNameAsync(actualDivisionId);

                // 5) Если в запросе переданы выбранные позиции (SelectedItems), фильтруем и/или упорядочиваем
                var selectedDocs = request.SelectedItems ?? new List<string>();
                if (selectedDocs.Count > 0)
                {
                    // 5.1) Оставляем только те, что в списке selectedDocs (если они есть в workItems)
                    // 5.2) Сортируем в порядке selectedDocs
                    var filtered = workItems
                        .Where(w => selectedDocs.Contains(w.DocumentNumber))
                        .OrderBy(w => selectedDocs.IndexOf(w.DocumentNumber))
                        .ToList();
                    workItems = filtered;
                }
                else
                {
                    // Если ничего не выбрано - берём все (как в Razor)
                    // Можно оставить как есть, workItems уже загружены
                }

                // 6) Генерируем нужный тип отчёта
                string format = request.Format?.ToLower() ?? "pdf";

                if (format == "pdf")
                {
                    var pdfBytes = ReportGenerator.GeneratePdf(
                        // конвертируем WorkItemDto -> WorkItem (т.к. методы ReportGenerator* используют старый тип),
                        // но у нас, по сути, поля те же. Можно написать маппер вручную:
                        workItems.Select(x => new Domain.Entities.WorkItem
                        {
                            DocumentNumber = x.DocumentNumber,
                            DocumentName = x.DocumentName,
                            WorkName = x.WorkName,
                            Executor = x.Executor,
                            Controller = x.Controller,
                            Approver = x.Approver,
                            PlanDate = x.PlanDate == null ? (DateTime?)null : x.PlanDate,
                            Korrect1 = x.Korrect1 == null ? (DateTime?)null : x.Korrect1,
                            Korrect2 = x.Korrect2 == null ? (DateTime?)null : x.Korrect2,
                            Korrect3 = x.Korrect3 == null ? (DateTime?)null : x.Korrect3,
                            FactDate = x.FactDate == null ? (DateTime?)null : x.FactDate
                        }).ToList(),
                        $"Сдаточный чек от {DateTime.Now:dd.MM.yyyy}",
                        depName
                    );

                    return File(pdfBytes, "application/pdf", $"Check_{DateTime.Now:yyyyMMdd}.pdf");
                }
                else if (format == "excel")
                {
                    var excelBytes = ReportGeneratorExcel.GenerateExcel(
                        workItems.Select(x => new Domain.Entities.WorkItem
                        {
                            DocumentNumber = x.DocumentNumber,
                            DocumentName = x.DocumentName,
                            WorkName = x.WorkName,
                            Executor = x.Executor,
                            Controller = x.Controller,
                            Approver = x.Approver,
                            PlanDate = x.PlanDate == null ? (DateTime?)null : x.PlanDate,
                            Korrect1 = x.Korrect1 == null ? (DateTime?)null : x.Korrect1,
                            Korrect2 = x.Korrect2 == null ? (DateTime?)null : x.Korrect2,
                            Korrect3 = x.Korrect3 == null ? (DateTime?)null : x.Korrect3,
                            FactDate = x.FactDate == null ? (DateTime?)null : x.FactDate
                        }).ToList(),
                        $"Сдаточный чек от {DateTime.Now:dd.MM.yyyy}",
                        depName
                    );
                    return File(
                        excelBytes,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Check_{DateTime.Now:yyyyMMdd}.xlsx"
                    );
                }
                else if (format == "word")
                {
                    var docBytes = ReportGeneratorWord.GenerateWord(
                        workItems.Select(x => new Domain.Entities.WorkItem
                        {
                            DocumentNumber = x.DocumentNumber,
                            DocumentName = x.DocumentName,
                            WorkName = x.WorkName,
                            Executor = x.Executor,
                            Controller = x.Controller,
                            Approver = x.Approver,
                            PlanDate = x.PlanDate == null ? (DateTime?)null : x.PlanDate,
                            Korrect1 = x.Korrect1 == null ? (DateTime?)null : x.Korrect1,
                            Korrect2 = x.Korrect2 == null ? (DateTime?)null : x.Korrect2,
                            Korrect3 = x.Korrect3 == null ? (DateTime?)null : x.Korrect3,
                            FactDate = x.FactDate == null ? (DateTime?)null : x.FactDate
                        }).ToList(),
                        $"Сдаточный чек от {DateTime.Now:dd.MM.yyyy}",
                        depName
                    );
                    return File(
                        docBytes,
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        $"Check_{DateTime.Now:yyyyMMdd}.docx"
                    );
                }
                else
                {
                    // Если формат неизвестен, просто вернём BadRequest
                    return BadRequest("Неизвестный формат экспорта.");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Ошибка при экспорте: " + ex.Message);
            }
        }
    }
}