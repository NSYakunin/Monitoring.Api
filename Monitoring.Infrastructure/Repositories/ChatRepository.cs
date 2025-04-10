using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Data;
using Monitoring.Infrastructure.Data.ScaffoldModels;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        // ------------------------
        // Маппинг EF -> DTO
        // ------------------------
        private ChatMessageDto MapToDto(ChatMessage ef)
        {
            return new ChatMessageDto
            {
                Id = ef.Id,
                FromUserId = ef.FromUserId,
                ToUserId = ef.ToUserId,
                GroupId = ef.GroupId,
                MessageText = ef.MessageText,
                CreatedAt = ef.CreatedAt,
                IsDeleted = ef.IsDeleted
            };
        }

        private ChatGroupDto MapToDto(ChatGroup ef)
        {
            return new ChatGroupDto
            {
                Id = ef.Id,
                GroupName = ef.GroupName,
                CreatedAt = ef.CreatedAt,
                ChatGroupUsers = ef.ChatGroupUsers
                    .Select(MapToDto)
                    .ToList()
            };
        }

        private ChatGroupUserDto MapToDto(ChatGroupUser ef)
        {
            return new ChatGroupUserDto
            {
                Id = ef.Id,
                GroupId = ef.GroupId,
                UserId = ef.UserId,
                IsAdmin = ef.IsAdmin
            };
        }

        private ChatUserRelationshipDto MapToDto(ChatUserRelationship ef)
        {
            return new ChatUserRelationshipDto
            {
                Id = ef.Id,
                UserId = ef.UserId,
                OtherUserId = ef.OtherUserId,
                IsFriend = ef.IsFriend,
                IsBlocked = ef.IsBlocked
            };
        }

        private UserChatDto MapToDto(User ef)
        {
            return new UserChatDto
            {
                IdUser = ef.IdUser,
                Name = ef.Name,
                SmallName = ef.SmallName,
                IdDivision = ef.IdDivision,
                Password = ef.Password,
                IdTypeUser = ef.IdTypeUser,
                Isvalid = ef.Isvalid
            };
        }

        // ------------------------
        // Маппинг DTO -> EF
        // ------------------------
        private ChatMessage MapToEf(ChatMessageDto dto)
        {
            return new ChatMessage
            {
                Id = dto.Id,
                FromUserId = dto.FromUserId,
                ToUserId = dto.ToUserId,
                GroupId = dto.GroupId,
                MessageText = dto.MessageText,
                CreatedAt = dto.CreatedAt,
                IsDeleted = dto.IsDeleted
            };
        }

        private ChatGroup MapToEf(ChatGroupDto dto)
        {
            return new ChatGroup
            {
                Id = dto.Id,
                GroupName = dto.GroupName,
                CreatedAt = dto.CreatedAt
            };
        }

        private ChatGroupUser MapToEf(ChatGroupUserDto dto)
        {
            return new ChatGroupUser
            {
                Id = dto.Id,
                GroupId = dto.GroupId,
                UserId = dto.UserId,
                IsAdmin = dto.IsAdmin
            };
        }

        private ChatUserRelationship MapToEf(ChatUserRelationshipDto dto)
        {
            return new ChatUserRelationship
            {
                Id = dto.Id,
                UserId = dto.UserId,
                OtherUserId = dto.OtherUserId,
                IsFriend = dto.IsFriend,
                IsBlocked = dto.IsBlocked
            };
        }

        // ==============================
        // Сообщения
        // ==============================
        public async Task<ChatMessageDto> InsertMessageAsync(ChatMessageDto message)
        {
            var efMessage = MapToEf(message);
            _context.ChatMessages.Add(efMessage);
            await _context.SaveChangesAsync();

            // Обновляем Id в DTO
            message.Id = efMessage.Id;
            return message;
        }

        public async Task<ChatMessageDto?> FindMessageByIdAsync(long messageId)
        {
            var ef = await _context.ChatMessages.FindAsync(messageId);
            if (ef == null) return null;
            return MapToDto(ef);
        }

        public async Task<List<ChatMessageDto>> GetPrivateMessagesAsync(int userId, int otherUserId)
        {
            var efMessages = await _context.ChatMessages
                .Where(m => !m.IsDeleted &&
                            ((m.FromUserId == userId && m.ToUserId == otherUserId)
                              || (m.FromUserId == otherUserId && m.ToUserId == userId)))
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            return efMessages.Select(MapToDto).ToList();
        }

        public async Task<List<ChatMessageDto>> GetGroupMessagesAsync(int groupId)
        {
            var efMessages = await _context.ChatMessages
                .Where(m => !m.IsDeleted && m.GroupId == groupId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            return efMessages.Select(MapToDto).ToList();
        }

        public async Task<List<ChatMessageDto>> GetAllMessagesByPairAsync(int userA, int userB)
        {
            var efMessages = await _context.ChatMessages
                .Where(m => (m.FromUserId == userA && m.ToUserId == userB)
                         || (m.FromUserId == userB && m.ToUserId == userA))
                .ToListAsync();

            return efMessages.Select(MapToDto).ToList();
        }

        public async Task<List<ChatMessageDto>> GetAllMessagesByGroupAsync(int groupId)
        {
            var efMessages = await _context.ChatMessages
                .Where(m => m.GroupId == groupId)
                .ToListAsync();

            return efMessages.Select(MapToDto).ToList();
        }

        // ==============================
        // Отношения (друзья/блок)
        // ==============================
        public async Task<ChatUserRelationshipDto?> GetUserRelationshipAsync(int userId, int otherUserId)
        {
            var efRel = await _context.ChatUserRelationships
                .FirstOrDefaultAsync(r => r.UserId == userId && r.OtherUserId == otherUserId);

            if (efRel == null) return null;
            return MapToDto(efRel);
        }

        public async Task InsertRelationshipAsync(ChatUserRelationshipDto rel)
        {
            var efRel = MapToEf(rel);
            _context.ChatUserRelationships.Add(efRel);
            await _context.SaveChangesAsync();

            // Обновляем Id в DTO
            rel.Id = efRel.Id;
        }

        public async Task<List<UserChatDto>> GetFriendsAsync(int userId)
        {
            var query = from r in _context.ChatUserRelationships
                        join u in _context.Users on r.OtherUserId equals u.IdUser
                        where r.UserId == userId && r.IsFriend == true
                        select u;

            var efUsers = await query.ToListAsync();
            return efUsers.Select(MapToDto).ToList();
        }

        // ==============================
        // Группы
        // ==============================
        public async Task<ChatGroupDto> InsertGroupAsync(ChatGroupDto group)
        {
            var efGroup = MapToEf(group);
            _context.ChatGroups.Add(efGroup);
            await _context.SaveChangesAsync();

            group.Id = efGroup.Id;
            return group;
        }

        public async Task<ChatGroupDto?> FindGroupByIdAsync(int groupId)
        {
            var efGroup = await _context.ChatGroups.FindAsync(groupId);
            if (efGroup == null) return null;
            return MapToDto(efGroup);
        }

        public async Task<ChatGroupUserDto> InsertGroupUserAsync(ChatGroupUserDto groupUser)
        {
            var efGroupUser = MapToEf(groupUser);
            _context.ChatGroupUsers.Add(efGroupUser);
            await _context.SaveChangesAsync();

            groupUser.Id = efGroupUser.Id;
            return groupUser;
        }

        public async Task<ChatGroupUserDto?> GetGroupUserAsync(int groupId, int userId)
        {
            var efGroupUser = await _context.ChatGroupUsers
                .FirstOrDefaultAsync(gu => gu.GroupId == groupId && gu.UserId == userId);

            if (efGroupUser == null) return null;
            return MapToDto(efGroupUser);
        }

        public void RemoveGroupUser(ChatGroupUserDto groupUserDto)
        {
            // Для удаления нужно получить EF-модель
            var ef = MapToEf(groupUserDto);

            // Если объект не трекается - Attach
            _context.ChatGroupUsers.Attach(ef);
            _context.ChatGroupUsers.Remove(ef);
        }

        // ==============================
        // Общие
        // ==============================
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}