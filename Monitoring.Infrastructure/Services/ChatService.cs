using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Сервис для чата. 
    /// Здесь хранится бизнес-логика: проверки блокировки, проверка прав, маппинг в DTO (при необходимости).
    /// Доступ к БД осуществляется через IChatRepository.
    /// </summary>
    public class ChatService : IChatService
    {
        private readonly IChatRepository _chatRepository;

        public ChatService(IChatRepository chatRepository)
        {
            _chatRepository = chatRepository;
        }

        // -------------------------
        // Получаем список друзей
        // -------------------------
        public async Task<List<UserChatDto>> GetFriendsAsync(int userId)
        {
            // Используем метод репозитория для получения DTO пользователей
            return await _chatRepository.GetFriendsAsync(userId);
        }

        // -------------------------
        // Отправка сообщения
        // -------------------------
        public async Task<ChatMessageDto> SendMessageAsync(int fromUserId, int? toUserId, int? groupId, string message)
        {
            // Проверка блокировки, если это личное сообщение
            if (toUserId.HasValue)
            {
                bool blocked = await IsBlockedAsync(toUserId.Value, fromUserId);
                if (blocked)
                    throw new Exception("Вы не можете отправить сообщение — пользователь вас заблокировал.");
            }

            // Создаём DTO сообщения
            var msgDto = new ChatMessageDto
            {
                FromUserId = fromUserId,
                ToUserId = toUserId,
                GroupId = groupId,
                MessageText = message,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            // Сохраняем через репозиторий
            var savedMsg = await _chatRepository.InsertMessageAsync(msgDto);
            return savedMsg;
        }

        // -------------------------
        // Получение приватных сообщений
        // -------------------------
        public async Task<List<ChatMessageDto>> GetPrivateMessagesAsync(int userId, int otherUserId)
        {
            return await _chatRepository.GetPrivateMessagesAsync(userId, otherUserId);
        }

        // -------------------------
        // Получение групповых сообщений
        // -------------------------
        public async Task<List<ChatMessageDto>> GetGroupMessagesAsync(int groupId)
        {
            return await _chatRepository.GetGroupMessagesAsync(groupId);
        }

        // -------------------------
        // Удаление сообщения
        // -------------------------
        public async Task DeleteMessageAsync(long messageId, int requestingUserId)
        {
            var msg = await _chatRepository.FindMessageByIdAsync(messageId);
            if (msg == null) return; // сообщение не найдено

            // Проверка владельца
            if (msg.FromUserId != requestingUserId)
            {
                // Если это групповое сообщение, проверяем админство
                if (msg.GroupId.HasValue)
                {
                    var grpUser = await _chatRepository.GetGroupUserAsync(msg.GroupId.Value, requestingUserId);
                    if (grpUser == null || !grpUser.IsAdmin)
                        throw new Exception("Недостаточно прав для удаления сообщения.");
                }
                else
                {
                    throw new Exception("Недостаточно прав для удаления чужого сообщения.");
                }
            }

            // Помечаем как удалённое
            msg.IsDeleted = true;

            // Сохраняем изменение (через репозиторий или напрямую)
            await _chatRepository.SaveChangesAsync();
        }

        // -------------------------
        // Очистить историю переписки (приватной)
        // -------------------------
        public async Task ClearPrivateHistoryAsync(int userId, int otherUserId)
        {
            // Загружаем все сообщения (без фильтра IsDeleted)
            var msgs = await _chatRepository.GetAllMessagesByPairAsync(userId, otherUserId);

            foreach (var m in msgs)
            {
                m.IsDeleted = true;
            }

            await _chatRepository.SaveChangesAsync();
        }

        // -------------------------
        // Очистить историю переписки (групповой)
        // -------------------------
        public async Task ClearGroupHistoryAsync(int groupId, int requestingUserId)
        {
            // Проверяем, что requestingUserId является админом
            var grpUser = await _chatRepository.GetGroupUserAsync(groupId, requestingUserId);
            if (grpUser == null || !grpUser.IsAdmin)
                throw new Exception("Недостаточно прав для очистки истории группы.");

            // Загружаем все сообщения группы (без учёта IsDeleted)
            var msgs = await _chatRepository.GetAllMessagesByGroupAsync(groupId);
            foreach (var m in msgs)
            {
                m.IsDeleted = true;
            }

            await _chatRepository.SaveChangesAsync();
        }

        // -------------------------
        // Добавить друга
        // -------------------------
        public async Task AddFriendAsync(int userId, int friendUserId)
        {
            if (userId == friendUserId)
                throw new Exception("Нельзя добавить в друзья самого себя.");

            var rel = await _chatRepository.GetUserRelationshipAsync(userId, friendUserId);
            if (rel == null)
            {
                var newRel = new ChatUserRelationshipDto
                {
                    UserId = userId,
                    OtherUserId = friendUserId,
                    IsFriend = true,
                    IsBlocked = false
                };
                await _chatRepository.InsertRelationshipAsync(newRel);
            }
            else
            {
                rel.IsFriend = true;
                rel.IsBlocked = false;
                await _chatRepository.SaveChangesAsync();
            }
        }

        // -------------------------
        // Удалить друга
        // -------------------------
        public async Task RemoveFriendAsync(int userId, int friendUserId)
        {
            var rel = await _chatRepository.GetUserRelationshipAsync(userId, friendUserId);
            if (rel != null)
            {
                rel.IsFriend = false;
                await _chatRepository.SaveChangesAsync();
            }
        }

        // -------------------------
        // Заблокировать пользователя
        // -------------------------
        public async Task BlockUserAsync(int userId, int blockedUserId)
        {
            if (userId == blockedUserId)
                throw new Exception("Нельзя заблокировать самого себя.");

            var rel = await _chatRepository.GetUserRelationshipAsync(userId, blockedUserId);
            if (rel == null)
            {
                var newRel = new ChatUserRelationshipDto
                {
                    UserId = userId,
                    OtherUserId = blockedUserId,
                    IsFriend = false,
                    IsBlocked = true
                };
                await _chatRepository.InsertRelationshipAsync(newRel);
            }
            else
            {
                rel.IsFriend = false;
                rel.IsBlocked = true;
                await _chatRepository.SaveChangesAsync();
            }
        }

        // -------------------------
        // Разблокировать пользователя
        // -------------------------
        public async Task UnblockUserAsync(int userId, int blockedUserId)
        {
            var rel = await _chatRepository.GetUserRelationshipAsync(userId, blockedUserId);
            if (rel != null)
            {
                rel.IsBlocked = false;
                await _chatRepository.SaveChangesAsync();
            }
        }

        // -------------------------
        // Проверить, заблокирован ли userB со стороны userA
        // -------------------------
        public async Task<bool> IsBlockedAsync(int userA, int userB)
        {
            var rel = await _chatRepository.GetUserRelationshipAsync(userA, userB);
            return (rel != null && rel.IsBlocked);
        }

        // -------------------------
        // Создать группу
        // -------------------------
        public async Task<int> CreateGroupAsync(int ownerUserId, string groupName)
        {
            var groupDto = new ChatGroupDto
            {
                GroupName = groupName,
                CreatedAt = DateTime.UtcNow
            };

            var savedGroup = await _chatRepository.InsertGroupAsync(groupDto);

            // Добавляем владельца в группу как админа
            var guDto = new ChatGroupUserDto
            {
                GroupId = savedGroup.Id,
                UserId = ownerUserId,
                IsAdmin = true
            };
            await _chatRepository.InsertGroupUserAsync(guDto);

            return savedGroup.Id;
        }

        // -------------------------
        // Добавить пользователя в группу
        // -------------------------
        public async Task AddUserToGroupAsync(int groupId, int userId, bool isAdmin = false)
        {
            // Проверяем, что группа существует
            var group = await _chatRepository.FindGroupByIdAsync(groupId);
            if (group == null)
                throw new Exception("Группа не найдена.");

            // Ищем, не добавлен ли пользователь уже
            var existing = await _chatRepository.GetGroupUserAsync(groupId, userId);
            if (existing != null)
            {
                // Обновляем статус админа
                existing.IsAdmin = isAdmin;
                await _chatRepository.SaveChangesAsync();
            }
            else
            {
                var guDto = new ChatGroupUserDto
                {
                    GroupId = groupId,
                    UserId = userId,
                    IsAdmin = isAdmin
                };
                await _chatRepository.InsertGroupUserAsync(guDto);
            }
        }

        // -------------------------
        // Удалить пользователя из группы
        // -------------------------
        public async Task RemoveUserFromGroupAsync(int groupId, int userId, int requestingUserId)
        {
            // Проверяем, что requestingUserId имеет права (админ)
            var req = await _chatRepository.GetGroupUserAsync(groupId, requestingUserId);
            if (req == null || !req.IsAdmin)
                throw new Exception("Недостаточно прав.");

            var gu = await _chatRepository.GetGroupUserAsync(groupId, userId);
            if (gu != null)
            {
                // Удаляем участника
                _chatRepository.RemoveGroupUser(gu);
                await _chatRepository.SaveChangesAsync();
            }
        }
    }
}