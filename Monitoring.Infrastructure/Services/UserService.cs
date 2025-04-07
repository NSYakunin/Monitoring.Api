    // ------------------------------
    // Monitoring.Infrastructure\Services\UserService.cs
    // ------------------------------
    using Monitoring.Application.DTO;
    using Monitoring.Application.Interfaces;
    using Monitoring.Infrastructure.Data;
    using Monitoring.Infrastructure.Data.ScaffoldModels;
    using Microsoft.EntityFrameworkCore;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    namespace Monitoring.Infrastructure.Services
    {
        public class UserService : IUserService
        {
            private readonly MyDbContext _context;

            public UserService(MyDbContext context)
            {
                _context = context;
            }

            /// <summary>
            /// Возвращает список всех пользователей, кроме currentUserId.
            /// </summary>
            public async Task<List<UserDto>> GetAllUsersExceptAsync(int currentUserId)
            {
                var users = await _context.Users
                    .Where(u => u.IdUser != currentUserId)
                    .Select(u => new UserDto
                    {
                        UserId = u.IdUser,
                        UserName = u.SmallName ?? u.Name
                    })
                    .ToListAsync();

                return users;
            }
        }
    }