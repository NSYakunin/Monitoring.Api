using Microsoft.AspNetCore.Mvc;
using Monitoring.Application.Interfaces;
using Monitoring.Application.DTO;

namespace Monitoring.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkItemsController : ControllerBase
    {
        private readonly IWorkItemAppService _workItemAppService;

        public WorkItemsController(IWorkItemAppService workItemAppService)
        {
            _workItemAppService = workItemAppService;
        }

        /// <summary>
        /// Пример GET-метода, который возвращает список работ по указанному подразделению.
        /// </summary>
        /// <param name="divisionId">ID подразделения</param>
        /// <returns>Список работ (WorkItemDto)</returns>
        [HttpGet("{divisionId}")]
        public async Task<ActionResult<List<WorkItemDto>>> GetWorkItemsByDivision(int divisionId)
        {
            try
            {
                var items = await _workItemAppService.GetWorkItemsByDivisionAsync(divisionId);
                return Ok(items);
            }
            catch (Exception ex)
            {
                // Логирование ошибки (ex) при желании
                return StatusCode(500, $"Ошибка на сервере: {ex.Message}");
            }
        }
    }
}