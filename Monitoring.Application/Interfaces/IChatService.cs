using Monitoring.Application.DTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Интерфейс чат-сервиса.
    /// Содержит бизнес-логику (проверки, условия, создание DTO).
    /// </summary>
    public interface IChatService
    {
        Task<List<UserChatDto>> GetFriendsAsync(int userId);

        Task<ChatMessageDto> SendMessageAsync(int fromUserId, int? toUserId, int? groupId, string message);

        Task<List<ChatMessageDto>> GetPrivateMessagesAsync(int userId, int otherUserId);

        Task<List<ChatMessageDto>> GetGroupMessagesAsync(int groupId);

        Task DeleteMessageAsync(long messageId, int requestingUserId);

        Task ClearPrivateHistoryAsync(int userId, int otherUserId);

        Task ClearGroupHistoryAsync(int groupId, int requestingUserId);

        // Работа с друзьями/блокировкой
        Task AddFriendAsync(int userId, int friendUserId);
        Task RemoveFriendAsync(int userId, int friendUserId);
        Task BlockUserAsync(int userId, int blockedUserId);
        Task UnblockUserAsync(int userId, int blockedUserId);

        // Проверка блокировки
        Task<bool> IsBlockedAsync(int userA, int userB);

        // Группы
        Task<int> CreateGroupAsync(int ownerUserId, string groupName);
        Task AddUserToGroupAsync(int groupId, int userId, bool isAdmin = false);
        Task RemoveUserFromGroupAsync(int groupId, int userId, int requestingUserId);
    }
}