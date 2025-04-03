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
    public class ChatRepository : IChatRepository
    {
        private readonly MyDbContext _context;

        public ChatRepository(MyDbContext context)
        {
            _context = context;
        }

        // Пример метода на получение сообщений (личных):
        public async Task<List<ChatMessage>> GetPrivateMessagesAsync(int userId, int otherUserId)
        {
            // Выбираем сообщения, где (FromUserId==userId AND ToUserId==otherUserId) 
            // или (FromUserId==otherUserId AND ToUserId==userId)
            // + не удалённые
            return await _context.ChatMessages
                .Where(m => (
                    (m.FromUserId == userId && m.ToUserId == otherUserId) ||
                    (m.FromUserId == otherUserId && m.ToUserId == userId)
                ) && m.IsDeleted == false)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
        }

        // ... и прочие методы для работы с ChatMessages, ChatGroups, ChatUserRelationship ...
    }

    public interface IChatRepository
    {
        Task<List<ChatMessage>> GetPrivateMessagesAsync(int userId, int otherUserId);
        // ... добавить остальные методы ...
    }
}