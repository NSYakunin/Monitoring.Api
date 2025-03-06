// =========================
// NotificationService.cs
// (пример реализации INotificationService, 
//  его вы показывали, я добавил лишь комментарии)
// =========================
using Monitoring.Domain.Entities;

namespace Monitoring.Infrastructure.Services
{
    public interface INotificationService
    {

        Task<List<Notification>> GetActiveNotificationsAsync(int divisionId);

        Task DeactivateOldNotificationsAsync(int days);
    }
}