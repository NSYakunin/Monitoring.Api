using Monitoring.Application.Interfaces;
using Monitoring.Application.DTO;
using Monitoring.Domain.Chat;
using Monitoring.Infrastructure.Data.ScaffoldModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Monitoring.Infrastructure.Repositories;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Сервис для чата. 
    /// Здесь храним бизнес-логику: проверки блокировки, проверку прав, маппинг в DTO.
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
        public async Task<List<UserDto>> GetFriendsAsync(int userId)
        {
            // Используем метод репозитория для получения сущностей User,
            // которые являются друзьями (IsFriend = true).
            var friendEntities = await _chatRepository.GetFriendsAsync(userId);

            // Преобразуем в DTO
            return friendEntities
                .Select(u => new UserDto
                {
                    UserId = u.IdUser,
                    UserName = u.SmallName ?? u.Name
                })
                .ToList();
        }

        // -------------------------
        // Отправка сообщения
        // -------------------------
        public async Task<ChatMessageDto> SendMessageAsync(int fromUserId, int? toUserId, int? groupId, string message)
        {
            // Проверка блокировки, если это личное сообщение (toUserId.HasValue)
            if (toUserId.HasValue)
            {
                bool blocked = await IsBlockedAsync(toUserId.Value, fromUserId);
                if (blocked)
                    throw new Exception("Вы не можете отправить сообщение — пользователь вас заблокировал.");
            }

            // Создаём сущность ChatMessage
            var msg = new ChatMessage
            {
                FromUserId = fromUserId,
                ToUserId = toUserId,
                GroupId = groupId,
                MessageText = message,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            // Сохраняем в БД через репозиторий
            var savedMsg = await _chatRepository.InsertMessageAsync(msg);

            // Возвращаем DTO
            return new ChatMessageDto
            {
                Id = savedMsg.Id,
                FromUserId = savedMsg.FromUserId,
                ToUserId = savedMsg.ToUserId,
                GroupId = savedMsg.GroupId,
                MessageText = savedMsg.MessageText,
                CreatedAt = savedMsg.CreatedAt
            };
        }

        // -------------------------
        // Получение приватных сообщений
        // -------------------------
        public async Task<List<ChatMessageDto>> GetPrivateMessagesAsync(int userId, int otherUserId)
        {
            var messages = await _chatRepository.GetPrivateMessagesAsync(userId, otherUserId);

            // Маппим в DTO
            return messages
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    FromUserId = m.FromUserId,
                    ToUserId = m.ToUserId,
                    GroupId = m.GroupId,
                    MessageText = m.MessageText,
                    CreatedAt = m.CreatedAt
                })
                .ToList();
        }

        // -------------------------
        // Получение групповых сообщений
        // -------------------------
        public async Task<List<ChatMessageDto>> GetGroupMessagesAsync(int groupId)
        {
            var messages = await _chatRepository.GetGroupMessagesAsync(groupId);

            return messages
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    FromUserId = m.FromUserId,
                    ToUserId = m.ToUserId,
                    GroupId = m.GroupId,
                    MessageText = m.MessageText,
                    CreatedAt = m.CreatedAt
                })
                .ToList();
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

            // Сохраняем изменение
            await _chatRepository.SaveChangesAsync();
        }

        // -------------------------
        // Очистить историю переписки (приватной)
        // -------------------------
        public async Task ClearPrivateHistoryAsync(int userId, int otherUserId)
        {
            // Загружаем все сообщения (без IsDeleted = false фильтра),
            // чтобы пометить их удалёнными.
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

            // Загружаем все сообщения (без учёта IsDeleted),
            // чтобы пометить их удалёнными
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

            // Ищем запись отношения
            var rel = await _chatRepository.GetUserRelationshipAsync(userId, friendUserId);

            if (rel == null)
            {
                // Создаём новую запись
                var newRel = new ChatUserRelationship
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
                // Обновляем существующую
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
                var newRel = new ChatUserRelationship
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
        // Проверить, заблокирован ли userB userA
        // -------------------------
        public async Task<bool> IsBlockedAsync(int userA, int userB)
        {
            // userA -> userB
            var rel = await _chatRepository.GetUserRelationshipAsync(userA, userB);
            return (rel != null && rel.IsBlocked);
        }

        // -------------------------
        // Создать группу
        // -------------------------
        public async Task<int> CreateGroupAsync(int ownerUserId, string groupName)
        {
            var group = new ChatGroup
            {
                GroupName = groupName,
                CreatedAt = DateTime.UtcNow
            };

            // Сохраняем группу
            var savedGroup = await _chatRepository.InsertGroupAsync(group);

            // Добавляем владельца в группу как админа
            var gu = new ChatGroupUser
            {
                GroupId = savedGroup.Id,
                UserId = ownerUserId,
                IsAdmin = true
            };
            await _chatRepository.InsertGroupUserAsync(gu);

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
                existing.IsAdmin = isAdmin;
                await _chatRepository.SaveChangesAsync();
            }
            else
            {
                var gu = new ChatGroupUser
                {
                    GroupId = groupId,
                    UserId = userId,
                    IsAdmin = isAdmin
                };
                await _chatRepository.InsertGroupUserAsync(gu);
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