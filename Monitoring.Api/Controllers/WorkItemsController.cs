using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;

namespace Monitoring.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Требуем наличие JWT (Bearer-token) для всех методов (кроме особых случаев)
    public class WorkItemsController : ControllerBase
    {
        private readonly IWorkItemAppService _workItemAppService;

        public WorkItemsController(IWorkItemAppService workItemAppService)
        {
            _workItemAppService = workItemAppService;
        }

        /// <summary>
        /// GET /api/WorkItems
        /// Параметры запроса (query): ?startDate=2025-01-01&endDate=2025-02-01&executor=...&approver=...&search=... 
        /// divisionId берём из Claims, чтобы пользователь не ходил в чужой отдел.
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

                // Если вы хотите логики по умолчанию:
                // startDate = 2014-01-01, endDate = последний день текущего месяца.
                if (!startDate.HasValue)
                    startDate = new DateOnly(2014, 1, 1);

                if (!endDate.HasValue)
                {
                    var now = DateTime.Now;
                    // возьмём последний день месяца
                    endDate = new DateOnly(now.Year, now.Month, 1).AddMonths(1).AddDays(-1);
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
        /// (Демонстрационный метод, который вернёт список отделов, доступных пользователю,
        /// если вам нужно повторить логику "AllowedDivisions" из Razor. 
        /// Пока stub, возвращаем только "свой" divisionId. 
        /// </summary>
        [HttpGet("AllowedDivisions")]
        public ActionResult<List<int>> GetAllowedDivisions()
        {
            // Для демонстрации отдадим 1 значение:
            // divisionId, который записан в JWT токене текущего пользователя.
            var userDivClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;
            if (string.IsNullOrEmpty(userDivClaim))
            {
                return Forbid("Нет divisionId в токене");
            }
            int divisionId = int.Parse(userDivClaim);

            // В реальности здесь можно вызвать метод из UserSettingsService и вернуть реальный список.
            return new List<int> { divisionId, 555, 777, 123123, 3123 };
        }

        /// <summary>
        /// GET /api/WorkItems/Executors?divisionId=...
        /// Для загрузки списка исполнителей.
        /// </summary>
        [HttpGet("Executors")]
        public ActionResult<List<string>> GetExecutors([FromQuery] int divisionId)
        {
            // В реальном проекте – запрос в БД. Пока заглушка:
            var list = new List<string> { "Иванов И.И.", "Петров П.П.", "Сидоров С.С.", "Тракторенко И.И" };
            return list;
        }

        /// <summary>
        /// GET /api/WorkItems/Approvers?divisionId=...
        /// Для загрузки списка принимающих.
        /// </summary>
        [HttpGet("Approvers")]
        public ActionResult<List<string>> GetApprovers([FromQuery] int divisionId)
        {
            // Аналогично, пока заглушка.
            var list = new List<string> { "Главный Инженер", "Начальник цеха", "Начальник начальников" };
            return list;
        }
    }
}