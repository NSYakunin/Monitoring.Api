// =========================
// NotificationController.cs
// =========================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Monitoring.Application.Interfaces;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Claims;
using Monitoring.Infrastructure.Services;

namespace Monitoring.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // JWT авторизация
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly IUserSettingsService _userSettingsService;

        public NotificationsController(INotificationService notificationService,
                                       IUserSettingsService userSettingsService)
        {
            _notificationService = notificationService;
            _userSettingsService = userSettingsService;
        }

        /// <summary>
        /// Получить список активных уведомлений для конкретного подразделения
        /// GET /api/Notifications?divisionId=XX
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetActiveNotifications([FromQuery] int divisionId)
        {
            // Можно дополнительно проверять права на просмотр данного divisionId
            // при помощи IUserSettingsService, но опустим для простоты.

            // Деактивируем старые уведомления (например, старше 90 дней):
            await _notificationService.DeactivateOldNotificationsAsync(90);

            // Возвращаем актуальные
            var notes = await _notificationService.GetActiveNotificationsAsync(divisionId);
            return Ok(notes);
        }

        /// <summary>
        /// Пример ручного вызова "деактивации старых" уведомлений.
        /// POST /api/Notifications/DeactivateOld?days=90
        /// </summary>
        [HttpPost("DeactivateOld")]
        public async Task<IActionResult> DeactivateOld([FromQuery] int days)
        {
            await _notificationService.DeactivateOldNotificationsAsync(days);
            return Ok(new { success = true });
        }
    }
}