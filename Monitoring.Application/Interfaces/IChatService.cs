// ------------------------------------------------
// ФАЙЛ: Monitoring.Application\Interfaces\IChatService.cs
// ------------------------------------------------
using Monitoring.Application.DTO;
using Monitoring.Domain.Chat;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Monitoring.Application.Interfaces
{
    public interface IChatService
    {
        Task<List<UserDto>> GetFriendsAsync(int userId);
        // Отправить сообщение (личное или в группу)
        Task<ChatMessageDto> SendMessageAsync(int fromUserId, int? toUserId, int? groupId, string message);

        // Получить историю сообщений с конкретным пользователем
        Task<List<ChatMessageDto>> GetPrivateMessagesAsync(int userId, int otherUserId);

        // Получить историю сообщений в группе
        Task<List<ChatMessageDto>> GetGroupMessagesAsync(int groupId);

        // Удалить сообщение (мягкое удаление)
        Task DeleteMessageAsync(long messageId, int requestingUserId);

        // Очистить историю сообщений (полностью) в личном чате
        Task ClearPrivateHistoryAsync(int userId, int otherUserId);

        // Очистить историю в группе (только если пользователь - админ?)
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