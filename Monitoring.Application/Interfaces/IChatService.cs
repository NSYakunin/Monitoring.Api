using Monitoring.Application.DTO;
using Monitoring.Domain.Chat;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Интерфейс чатового сервиса.
    /// Содержит бизнес-логику (проверки, условия, создание DTO).
    /// </summary>
    public interface IChatService
    {
        Task<List<UserDto>> GetFriendsAsync(int userId);
        Task<ChatMessageDto> SendMessageAsync(int fromUserId, int? toUserId, int? groupId, string message);
        Task<List<ChatMessageDto>> GetPrivateMessagesAsync(int userId, int otherUserId);
        Task<List<ChatMessageDto>> GetGroupMessagesAsync(int groupId);
        Task DeleteMessageAsync(long messageId, int requestingUserId);
        Task ClearPrivateHistoryAsync(int userId, int otherUserId);
        Task ClearGroupHistoryAsync(int groupId, int requestingUserId);

        // --------------------
        // Работа с друзьями:
        // --------------------
        Task AddFriendAsync(int userId, int friendUserId);
        Task RemoveFriendAsync(int userId, int friendUserId);
        Task BlockUserAsync(int userId, int blockedUserId);
        Task UnblockUserAsync(int userId, int blockedUserId);
        // Проверить, заблокирован ли userB со стороны userA
        Task<bool> IsBlockedAsync(int userA, int userB);

        // --------------------
        // Группы (создать, добавить/убрать участника, удалить группу, и т.д.)
        // --------------------
        Task<int> CreateGroupAsync(int ownerUserId, string groupName);
        Task AddUserToGroupAsync(int groupId, int userId, bool isAdmin = false);
        Task RemoveUserFromGroupAsync(int groupId, int userId, int requestingUserId);
    }
}