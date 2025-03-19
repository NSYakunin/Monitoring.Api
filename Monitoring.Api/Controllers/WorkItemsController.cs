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
using Monitoring.Domain.Entities;

namespace Monitoring.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Требуем наличие JWT (Bearer-token)
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

        // --- (НОВЫЙ вариант: возвращаем PagedWorkItemsDto, а не просто List<WorkItemDto>) ---
        // Добавлены параметры pageNumber, pageSize.
        [HttpGet]
        public async Task<ActionResult<PagedWorkItemsDto>> GetFilteredWorkItems(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? executor,
            [FromQuery] string? approver,
            [FromQuery] string? search,
            [FromQuery] int? divisionId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50
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

            // Вызываем метод сервиса, который вернёт пагинированный результат.
            var pagedResult = await _workItemAppService.GetFilteredWorkItemsAsync(
                divisionId ?? homeDivId,
                startDate,
                endDate,
                executor,
                approver,
                search,
                userId,
                pageNumber,
                pageSize,
                userName // Передадим userName, чтоб внутри подсветить нужные строки.
            );

            return Ok(pagedResult);
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

                // Можно добавить логику: если хотим всегда иметь в списке "0" => "Все подразделения",
                // то возвращаем "0" вручную. Но можно и на фронтенде вставить <option value="0">...
                // Решим на уровне фронта. 
                // Или можно сделать так:
                if (!divisions.Contains(0))
                {
                    // Условно добавим "0" для "Все подразделения"
                    divisions.Insert(0, 0);
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

        [HttpPost("Export")]
        public async Task<IActionResult> Export([FromBody] ExportRequestDto request)
        {
            try
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var userName = User.Identity?.Name;
                var divIdClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(divIdClaim))
                {
                    return Forbid("Нет необходимых данных в токене (userId, userName, divisionId).");
                }

                int userId = int.Parse(userIdClaim);
                int homeDivisionId = int.Parse(divIdClaim);

                int actualDivisionId = request.DivisionId ?? homeDivisionId;

                var startDate = request.StartDate ?? new DateTime(2014, 1, 1);
                var endDate = request.EndDate ?? DateTime.Now;

                // Здесь нам не нужна постраничность, т.к. выгружаем целиком.
                // Поэтому берём все записи (можно передать pageNumber=1, pageSize=999999 или сделать отдельный метод).
                // Допустим, создадим вспомогательный метод в сервисе, который вернёт всё без пагинации:
                var workItems = await _workItemAppService.GetAllFilteredWithoutPaging(
                    actualDivisionId,
                    startDate,
                    endDate,
                    request.Executor,
                    request.Approver,
                    request.Search,
                    userId,
                    userName
                );

                // Узнаем "название" подразделения
                string depName = await _workItemAppService.GetDevNameAsync(actualDivisionId);

                // Фильтруем выбранные
                var selectedDocs = request.SelectedItems ?? new List<string>();
                if (selectedDocs.Count > 0)
                {
                    var filtered = workItems
                        .Where(w => selectedDocs.Contains(w.DocumentNumber))
                        .OrderBy(w => selectedDocs.IndexOf(w.DocumentNumber))
                        .ToList();
                    workItems = filtered;
                }


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