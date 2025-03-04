using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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

        // Пример защищённого метода (требует Bearer-токен, выданный AuthController'ом):
        [HttpGet("{divisionId}")]
        [Authorize]
        public async Task<ActionResult<List<WorkItemDto>>> GetWorkItemsByDivision()
        {
            try
            {
                // Например, мы можем сверить: 
                // "а совпадает ли divisionId из пути с divisionId у текущего пользователя?"
                var userDivClaim = User.Claims.FirstOrDefault(c => c.Type == "divisionId")?.Value;

                if (userDivClaim == null)
                {
                    return Forbid("У токена нет claim 'divisionId'.");
                }

                int userDiv = int.Parse(userDivClaim);

                //if (int.TryParse(userDivClaim, out var userDivId))
                //{
                //    // Если нужно, чтобы пользователь видел только свой отдел
                //    if (userDivId != divisionId)
                //    {
                //        return Forbid("Нет прав смотреть чужой отдел.");
                //    }
                //}

                var items = await _workItemAppService.GetWorkItemsByDivisionAsync(userDiv);
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Ошибка на сервере: {ex.Message}");
            }
        }
    }
}