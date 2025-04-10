// ------------------------------------------------
// ФАЙЛ: Monitoring.Infrastructure\Repositories\ChatRepository.cs
// ------------------------------------------------
using Monitoring.Application.Interfaces;
using Monitoring.Domain.Chat;
using Monitoring.Infrastructure.Data;
using Monitoring.Infrastructure.Data.ScaffoldModels;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Monitoring.Infrastructure.Repositories
{
    /// <summary>
    /// Репозиторий для работы с чатами, сообщениями, группами и др.
    /// Здесь только доступ к данным, без бизнес-проверок.
    /// </summary>
    public class ChatRepository : IChatRepository
    {
        private readonly MyDbContext _context;

        public ChatRepository(MyDbContext context)
        {
            _context = context;
        }

        // ==============================
        // Сообщения
        // ==============================
        public async Task<ChatMessage> InsertMessageAsync(ChatMessage message)
        {
            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();
            return message; // вернём с присвоенным Id
        }

        public async Task<ChatMessage> FindMessageByIdAsync(long messageId)
        {
            return await _context.ChatMessages.FindAsync(messageId);
        }

        public async Task<List<ChatMessage>> GetPrivateMessagesAsync(int userId, int otherUserId)
        {
            return await _context.ChatMessages
                .Where(m => !m.IsDeleted &&
                            ((m.FromUserId == userId && m.ToUserId == otherUserId)
                              || (m.FromUserId == otherUserId && m.ToUserId == userId)))
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<ChatMessage>> GetGroupMessagesAsync(int groupId)
        {
            return await _context.ChatMessages
                .Where(m => !m.IsDeleted && m.GroupId == groupId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<ChatMessage>> GetAllMessagesByPairAsync(int userA, int userB)
        {
            return await _context.ChatMessages
                .Where(m => (m.FromUserId == userA && m.ToUserId == userB)
                         || (m.FromUserId == userB && m.ToUserId == userA))
                .ToListAsync();
        }

        public async Task<List<ChatMessage>> GetAllMessagesByGroupAsync(int groupId)
        {
            return await _context.ChatMessages
                .Where(m => m.GroupId == groupId)
                .ToListAsync();
        }

        // ==============================
        // Отношения (друзья/блок)
        // ==============================
        public async Task<ChatUserRelationship> GetUserRelationshipAsync(int userId, int otherUserId)
        {
            return await _context.ChatUserRelationships
                .FirstOrDefaultAsync(r => r.UserId == userId && r.OtherUserId == otherUserId);
        }

        public async Task InsertRelationshipAsync(ChatUserRelationship rel)
        {
            _context.ChatUserRelationships.Add(rel);
            await _context.SaveChangesAsync();
        }

        public async Task<List<User>> GetFriendsAsync(int userId)
        {
            // Возвращаем список сущностей User, которые являются друзьями:
            return await (
                from r in _context.ChatUserRelationships
                join u in _context.Users on r.OtherUserId equals u.IdUser
                where r.UserId == userId && r.IsFriend == true
                select u
            ).ToListAsync();
        }

        // ==============================
        // Группы
        // ==============================
        public async Task<ChatGroup> InsertGroupAsync(ChatGroup group)
        {
            _context.ChatGroups.Add(group);
            await _context.SaveChangesAsync();
            return group;
        }

        public async Task<ChatGroup> FindGroupByIdAsync(int groupId)
        {
            return await _context.ChatGroups.FindAsync(groupId);
        }

        public async Task<ChatGroupUser> InsertGroupUserAsync(ChatGroupUser groupUser)
        {
            _context.ChatGroupUsers.Add(groupUser);
            await _context.SaveChangesAsync();
            return groupUser;
        }

        public async Task<ChatGroupUser> GetGroupUserAsync(int groupId, int userId)
        {
            return await _context.ChatGroupUsers
                .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);
        }

        public void RemoveGroupUser(ChatGroupUser groupUser)
        {
            _context.ChatGroupUsers.Remove(groupUser);
        }

        // ==============================
        // Общие
        // ==============================
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }

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
        Task<ChatMessage> InsertMessageAsync(ChatMessage message);

        /// <summary>
        /// Найти сообщение по его идентификатору.
        /// </summary>
        Task<ChatMessage> FindMessageByIdAsync(long messageId);

        /// <summary>
        /// Получить личные сообщения (userId <-> otherUserId),
        /// исключая удалённые.
        /// </summary>
        Task<List<ChatMessage>> GetPrivateMessagesAsync(int userId, int otherUserId);

        /// <summary>
        /// Получить групповые сообщения по groupId,
        /// исключая удалённые.
        /// </summary>
        Task<List<ChatMessage>> GetGroupMessagesAsync(int groupId);

        /// <summary>
        /// Получить все личные сообщения (в обе стороны), без фильтра на IsDeleted.
        /// Нужно для массовой очистки (чтобы затем их пометить IsDeleted = true).
        /// </summary>
        Task<List<ChatMessage>> GetAllMessagesByPairAsync(int userA, int userB);

        /// <summary>
        /// Получить все сообщения для группы (без фильтра IsDeleted).
        /// </summary>
        Task<List<ChatMessage>> GetAllMessagesByGroupAsync(int groupId);


        // ========== Отношения (друзья/блок) ==========

        /// <summary>
        /// Найти отношение userId -> otherUserId (Friend/Blocked).
        /// Возвращает null, если записи нет.
        /// </summary>
        Task<ChatUserRelationship> GetUserRelationshipAsync(int userId, int otherUserId);

        /// <summary>
        /// Добавить новую запись отношения (Friend/Blocked) в БД.
        /// </summary>
        Task InsertRelationshipAsync(ChatUserRelationship rel);

        /// <summary>
        /// Вернуть список пользователей, которые являются друзьями для userId.
        /// </summary>
        Task<List<User>> GetFriendsAsync(int userId);


        // ========== Группы ==========

        /// <summary>
        /// Создать новую группу.
        /// </summary>
        Task<ChatGroup> InsertGroupAsync(ChatGroup group);

        /// <summary>
        /// Найти группу по Id.
        /// </summary>
        Task<ChatGroup> FindGroupByIdAsync(int groupId);

        /// <summary>
        /// Добавить нового участника группы (ChatGroupUser).
        /// </summary>
        Task<ChatGroupUser> InsertGroupUserAsync(ChatGroupUser groupUser);

        /// <summary>
        /// Найти участника группы (строку ChatGroupUser).
        /// </summary>
        Task<ChatGroupUser> GetGroupUserAsync(int groupId, int userId);

        /// <summary>
        /// Удалить участника группы из контекста.
        /// </summary>
        void RemoveGroupUser(ChatGroupUser groupUser);

        // ========== Общие ==========

        /// <summary>
        /// Сохранить изменения в БД (обычно вызывается в конце, если репозиторий не делает SaveChangesAsync сам).
        /// </summary>
        Task SaveChangesAsync();
    }
}