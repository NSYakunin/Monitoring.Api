// ------------------------------
// Monitoring.Application\Interfaces\IUserService.cs
// ------------------------------
using System.Collections.Generic;
using System.Threading.Tasks;
using Monitoring.Application.DTO;

namespace Monitoring.Application.Interfaces
{
    public interface IUserService
    {
        Task<List<UserDto>> GetAllUsersExceptAsync(int currentUserId);
    }
}