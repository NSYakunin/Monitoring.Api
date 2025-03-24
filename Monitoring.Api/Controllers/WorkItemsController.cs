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

        /// <summary>
        /// Получить постранично отфильтрованные WorkItems. 
        /// ВСЕГДА возвращаем структуру PagedWorkItemsDto (items, currentPage, totalPages и т.д.).
        /// </summary>
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

            // Имя пользователя (smallName или логин) - как определено при выдаче токена
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                return Forbid("Нет userName");

            // Домашний отдел (из токена). Он показывается как родной для пользователя.
            var divIdClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;
            if (string.IsNullOrEmpty(divIdClaim))
                return Forbid("Нет divisionId в токене");
            int homeDivId = int.Parse(divIdClaim);

            // Вызываем метод сервиса, который вернёт пагинированный результат.
            var pagedResult = await _workItemAppService.GetFilteredWorkItemsAsync(
                divisionId ?? homeDivId,  // <-- Если divisionId не указан, берём домашний
                startDate,
                endDate,
                executor,
                approver,
                search,
                userId,
                pageNumber,
                pageSize,
                userName // Передадим userName, чтобы внутри подсветить нужные строки
            );

            return Ok(pagedResult);
        }

        /// <summary>
        /// Список айдишников отделов, доступных текущему пользователю.
        /// </summary>
        [HttpGet("AllowedDivisions")]
        public async Task<ActionResult<List<int>>> GetAllowedDivisions()
        {
            try
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (userIdClaim == null) return Forbid("Нет userId");
                int userId = int.Parse(userIdClaim);

                var divisions = await _userSettingsService.GetUserAllowedDivisionsAsync(userId);

                // Вытаскиваем из токена домашний отдел тоже, если его нет в списке — добавим.
                var divIdClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;
                if (!string.IsNullOrEmpty(divIdClaim))
                {
                    int homeDiv = int.Parse(divIdClaim);
                    if (!divisions.Contains(homeDiv))
                        divisions.Add(homeDiv);
                }

                // Часто нужно иметь "0" => "Все подразделения"
                // Можно вставить "0" в начало.
                if (!divisions.Contains(0))
                    divisions.Insert(0, 0);

                return Ok(divisions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Ошибка сервера: " + ex.Message);
            }
        }

        /// <summary>
        /// Список исполнителей в рамках divisionId (если 0 - можно вернуть всех или пусто, 
        /// но по задаче теперь не блокируем).
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
        /// Список принимающих в рамках divisionId (если 0 - можно вернуть всех, 
        /// по аналогии с Razor Pages).
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
        /// Получить название подразделения по ID
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

        /// <summary>
        /// Очистить кэш (по конкретному divisionId).
        /// </summary>
        [HttpPost("ClearCache")]
        public ActionResult ClearCache([FromQuery] int divisionId)
        {
            _workItemAppService.ClearCache(divisionId);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Экспорт в PDF/Excel/Word. 
        /// Если список SelectedItems пуст, выгружаем все (по фильтрам, но без пагинации). 
        /// </summary>
        [HttpPost("Export")]
        public async Task<IActionResult> Export([FromBody] ExportRequestDto request)
        {
            try
            {
                // Берём userId, userName, homeDivisionId из токена
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                var userName = User.Identity?.Name;
                var divIdClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) ||
                    string.IsNullOrEmpty(userName) ||
                    string.IsNullOrEmpty(divIdClaim))
                {
                    return Forbid("Нет необходимых данных в токене (userId, userName, divisionId).");
                }

                int userId = int.Parse(userIdClaim);
                int homeDivisionId = int.Parse(divIdClaim);

                int actualDivisionId = request.DivisionId ?? homeDivisionId;

                var startDate = request.StartDate ?? new DateTime(2014, 1, 1);
                var endDate = request.EndDate ?? DateTime.Now;

                // Берём все работы без пагинации
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

                // Если пользователь выбрал конкретные строки, то отфильтруем
                var selectedDocs = request.SelectedItems ?? new List<string>();
                if (selectedDocs.Count > 0)
                {
                    // Сохраняем порядок в соответствии с индексом в selectedDocs
                    workItems = workItems
                        .Where(w => selectedDocs.Contains(w.DocumentNumber))
                        .OrderBy(w => selectedDocs.IndexOf(w.DocumentNumber))
                        .ToList();
                }
                // Если список пуст => экспортируем всё (так по условию)

                // Готовим формат
                string format = request.Format?.ToLower() ?? "pdf";

                // Маппим WorkItemDto -> WorkItem для ReportGenerator (как раньше в Razor)
                var itemsForReport = workItems.Select(x => new Domain.Entities.WorkItem
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
                }).ToList();

                if (format == "pdf")
                {
                    var pdfBytes = ReportGenerator.GeneratePdf(
                        itemsForReport,
                        $"Сдаточный чек от {DateTime.Now:dd.MM.yyyy}",
                        depName
                    );
                    return File(pdfBytes, "application/pdf", $"Check_{DateTime.Now:yyyyMMdd}.pdf");
                }
                else if (format == "excel")
                {
                    var excelBytes = ReportGeneratorExcel.GenerateExcel(
                        itemsForReport,
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
                        itemsForReport,
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