﻿using Monitoring.Application.Interfaces;
using Monitoring.Domain.Chat;
using Monitoring.Infrastructure.Data;
using Monitoring.Infrastructure.Data.ScaffoldModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Monitoring.Application.DTO;

namespace Monitoring.Infrastructure.Services
{
    public class ChatService : IChatService
    {
        private readonly MyDbContext _context;

        public ChatService(MyDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Возвращаем список друзей текущего пользователя
        /// без использования Contains(...),
        /// чтобы не вызывать ошибку "WITH ..." на старых SQL Server.
        /// </summary>
        public async Task<List<UserDto>> GetFriendsAsync(int userId)
        {
            // Вместо friendIds.Contains(u.IdUser) — делаем обычный JOIN
            var friendsData = await (
                from r in _context.ChatUserRelationships
                join u in _context.Users on r.OtherUserId equals u.IdUser
                where r.UserId == userId && r.IsFriend == true
                select new UserDto
                {
                    UserId = u.IdUser,
                    UserName = u.SmallName ?? u.Name
                }
            ).ToListAsync();

            return friendsData;
        }

        public async Task<ChatMessageDto> SendMessageAsync(int fromUserId, int? toUserId, int? groupId, string message)
        {
            // Проверка на блокировку
            if (toUserId.HasValue)
            {
                bool blocked = await IsBlockedAsync(toUserId.Value, fromUserId);
                if (blocked)
                    throw new Exception("Вы не можете отправить сообщение — пользователь вас заблокировал.");
            }

            var msg = new ChatMessage
            {
                FromUserId = fromUserId,
                ToUserId = toUserId,
                GroupId = groupId,
                MessageText = message,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            _context.ChatMessages.Add(msg);
            await _context.SaveChangesAsync();

            return new ChatMessageDto
            {
                Id = msg.Id,
                FromUserId = fromUserId,
                ToUserId = toUserId,
                GroupId = groupId,
                MessageText = message,
                CreatedAt = msg.CreatedAt
            };
        }

        public async Task<List<ChatMessageDto>> GetPrivateMessagesAsync(int userId, int otherUserId)
        {
            var messages = await _context.ChatMessages
                .Where(m => !m.IsDeleted && (
                    (m.FromUserId == userId && m.ToUserId == otherUserId)
                    || (m.FromUserId == otherUserId && m.ToUserId == userId)
                ))
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            return messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                FromUserId = m.FromUserId,
                ToUserId = m.ToUserId,
                GroupId = m.GroupId,
                MessageText = m.MessageText,
                CreatedAt = m.CreatedAt
            }).ToList();
        }

        public async Task<List<ChatMessageDto>> GetGroupMessagesAsync(int groupId)
        {
            var messages = await _context.ChatMessages
                .Where(m => !m.IsDeleted && m.GroupId == groupId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            return messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                FromUserId = m.FromUserId,
                ToUserId = m.ToUserId,
                GroupId = m.GroupId,
                MessageText = m.MessageText,
                CreatedAt = m.CreatedAt
            }).ToList();
        }

        public async Task DeleteMessageAsync(long messageId, int requestingUserId)
        {
            var msg = await _context.ChatMessages.FindAsync(messageId);
            if (msg == null) return;

            // Проверка владельца
            if (msg.FromUserId != requestingUserId)
            {
                // Если это групповое сообщение, можно проверить админство
                if (msg.GroupId.HasValue)
                {
                    var grpUser = await _context.ChatGroupUsers
                        .FirstOrDefaultAsync(gu => gu.GroupId == msg.GroupId.Value && gu.UserId == requestingUserId);
                    if (grpUser == null || !grpUser.IsAdmin)
                        throw new Exception("Недостаточно прав для удаления сообщения.");
                }
                else
                {
                    throw new Exception("Недостаточно прав для удаления чужого сообщения.");
                }
            }

            msg.IsDeleted = true;
            await _context.SaveChangesAsync();
        }

        public async Task ClearPrivateHistoryAsync(int userId, int otherUserId)
        {
            var msgs = await _context.ChatMessages
                .Where(m => (
                    (m.FromUserId == userId && m.ToUserId == otherUserId)
                    || (m.FromUserId == otherUserId && m.ToUserId == userId)
                ))
                .ToListAsync();

            foreach (var m in msgs)
            {
                m.IsDeleted = true;
            }
            await _context.SaveChangesAsync();
        }

        public async Task ClearGroupHistoryAsync(int groupId, int requestingUserId)
        {
            var grpUser = await _context.ChatGroupUsers
                .FirstOrDefaultAsync(g => g.GroupId == groupId && g.UserId == requestingUserId);
            if (grpUser == null || !grpUser.IsAdmin)
                throw new Exception("Недостаточно прав для очистки истории группы.");

            var msgs = await _context.ChatMessages
                .Where(m => m.GroupId == groupId)
                .ToListAsync();
            foreach (var m in msgs)
            {
                m.IsDeleted = true;
            }
            await _context.SaveChangesAsync();
        }

        // -------------------------
        // Работа с друзьями/блокировкой
        // -------------------------
        public async Task AddFriendAsync(int userId, int friendUserId)
        {
            if (userId == friendUserId)
                throw new Exception("Нельзя добавить в друзья самого себя.");

            var rel = await _context.ChatUserRelationships
                .FirstOrDefaultAsync(r =>
                    (r.UserId == userId && r.OtherUserId == friendUserId)
                );
            if (rel == null)
            {
                rel = new ChatUserRelationship
                {
                    UserId = userId,
                    OtherUserId = friendUserId,
                    IsFriend = true,
                    IsBlocked = false
                };
                _context.ChatUserRelationships.Add(rel);
            }
            else
            {
                rel.IsFriend = true;
                rel.IsBlocked = false;
            }
            await _context.SaveChangesAsync();
        }

        public async Task RemoveFriendAsync(int userId, int friendUserId)
        {
            var rel = await _context.ChatUserRelationships
                .FirstOrDefaultAsync(r =>
                    r.UserId == userId && r.OtherUserId == friendUserId
                );
            if (rel != null)
            {
                rel.IsFriend = false;
                await _context.SaveChangesAsync();
            }
        }

        public async Task BlockUserAsync(int userId, int blockedUserId)
        {
            if (userId == blockedUserId)
                throw new Exception("Нельзя заблокировать самого себя.");

            var rel = await _context.ChatUserRelationships
                .FirstOrDefaultAsync(r =>
                    r.UserId == userId && r.OtherUserId == blockedUserId
                );
            if (rel == null)
            {
                rel = new ChatUserRelationship
                {
                    UserId = userId,
                    OtherUserId = blockedUserId,
                    IsFriend = false,
                    IsBlocked = true
                };
                _context.ChatUserRelationships.Add(rel);
            }
            else
            {
                rel.IsFriend = false;
                rel.IsBlocked = true;
            }
            await _context.SaveChangesAsync();
        }

        public async Task UnblockUserAsync(int userId, int blockedUserId)
        {
            var rel = await _context.ChatUserRelationships
                .FirstOrDefaultAsync(r =>
                    r.UserId == userId && r.OtherUserId == blockedUserId
                );
            if (rel != null)
            {
                rel.IsBlocked = false;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsBlockedAsync(int userA, int userB)
        {
            var rel = await _context.ChatUserRelationships
                .FirstOrDefaultAsync(r =>
                    r.UserId == userA && r.OtherUserId == userB
                );
            return (rel != null && rel.IsBlocked);
        }

        // -------------------------
        // Группы
        // -------------------------
        public async Task<int> CreateGroupAsync(int ownerUserId, string groupName)
        {
            var group = new ChatGroup
            {
                GroupName = groupName,
                CreatedAt = DateTime.UtcNow
            };
            _context.ChatGroups.Add(group);
            await _context.SaveChangesAsync();

            // сразу добавляем владельца в группу как админа
            var gu = new ChatGroupUser
            {
                GroupId = group.Id,
                UserId = ownerUserId,
                IsAdmin = true
            };
            _context.ChatGroupUsers.Add(gu);
            await _context.SaveChangesAsync();

            return group.Id;
        }

        public async Task AddUserToGroupAsync(int groupId, int userId, bool isAdmin = false)
        {
            var group = await _context.ChatGroups.FindAsync(groupId);
            if (group == null)
                throw new Exception("Группа не найдена.");

            var existing = await _context.ChatGroupUsers
                .FirstOrDefaultAsync(x => x.GroupId == groupId && x.UserId == userId);
            if (existing != null)
            {
                existing.IsAdmin = isAdmin;
            }
            else
            {
                var gu = new ChatGroupUser
                {
                    GroupId = groupId,
                    UserId = userId,
                    IsAdmin = isAdmin
                };
                _context.ChatGroupUsers.Add(gu);
            }
            await _context.SaveChangesAsync();
        }

        public async Task RemoveUserFromGroupAsync(int groupId, int userId, int requestingUserId)
        {
            var req = await _context.ChatGroupUsers
                .FirstOrDefaultAsync(x => x.GroupId == groupId && x.UserId == requestingUserId);
            if (req == null || !req.IsAdmin)
                throw new Exception("Недостаточно прав.");

            var gu = await _context.ChatGroupUsers
                .FirstOrDefaultAsync(x => x.GroupId == groupId && x.UserId == userId);
            if (gu != null)
            {
                _context.ChatGroupUsers.Remove(gu);
                await _context.SaveChangesAsync();
            }
        }
    }
}