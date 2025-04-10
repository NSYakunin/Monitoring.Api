using Monitoring.Application.DTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Интерфейс репозитория для работы с чатами, сообщениями, группами, отношениями пользователей (друзья/блок).
    /// Отвечает за операции с БД (Create, Read, Update, Delete).
    /// Не должен содержать бизнес-логику.
    /// </summary>
    public interface IChatRepository
    {
        // ========== Сообщения ==========

        /// <summary>
        /// Сохранить новое сообщение в БД.
        /// </summary>
        Task<ChatMessageDto> InsertMessageAsync(ChatMessageDto message);

        /// <summary>
        /// Найти сообщение по его идентификатору.
        /// </summary>
        Task<ChatMessageDto?> FindMessageByIdAsync(long messageId);

        /// <summary>
        /// Получить личные сообщения (userId <-> otherUserId), исключая удалённые.
        /// </summary>
        Task<List<ChatMessageDto>> GetPrivateMessagesAsync(int userId, int otherUserId);

        /// <summary>
        /// Получить групповые сообщения по groupId, исключая удалённые.
        /// </summary>
        Task<List<ChatMessageDto>> GetGroupMessagesAsync(int groupId);

        /// <summary>
        /// Получить все личные сообщения (в обе стороны), без учёта IsDeleted.
        /// </summary>
        Task<List<ChatMessageDto>> GetAllMessagesByPairAsync(int userA, int userB);

        /// <summary>
        /// Получить все сообщения для группы (без учёта IsDeleted).
        /// </summary>
        Task<List<ChatMessageDto>> GetAllMessagesByGroupAsync(int groupId);

        // ========== Отношения (друзья/блок) ==========

        /// <summary>
        /// Найти отношение userId -> otherUserId (Friend/Blocked).
        /// Возвращает null, если записи нет.
        /// </summary>
        Task<ChatUserRelationshipDto?> GetUserRelationshipAsync(int userId, int otherUserId);

        /// <summary>
        /// Добавить новую запись отношения (Friend/Blocked) в БД.
        /// </summary>
        Task InsertRelationshipAsync(ChatUserRelationshipDto rel);

        /// <summary>
        /// Вернуть список пользователей, которые являются друзьями для userId.
        /// </summary>
        Task<List<UserChatDto>> GetFriendsAsync(int userId);

        // ========== Группы ==========

        /// <summary>
        /// Создать новую группу.
        /// </summary>
        Task<ChatGroupDto> InsertGroupAsync(ChatGroupDto group);

        /// <summary>
        /// Найти группу по Id.
        /// </summary>
        Task<ChatGroupDto?> FindGroupByIdAsync(int groupId);

        /// <summary>
        /// Добавить нового участника группы (ChatGroupUserDto).
        /// </summary>
        Task<ChatGroupUserDto> InsertGroupUserAsync(ChatGroupUserDto groupUser);

        /// <summary>
        /// Найти участника группы по groupId и userId.
        /// Возвращает null, если не найден.
        /// </summary>
        Task<ChatGroupUserDto?> GetGroupUserAsync(int groupId, int userId);

        /// <summary>
        /// Удалить участника группы.
        /// </summary>
        void RemoveGroupUser(ChatGroupUserDto groupUserDto);

        // ========== Общие ==========

        /// <summary>
        /// Сохранить изменения в БД (обычно вызывается в конце).
        /// </summary>
        Task SaveChangesAsync();
    }
}