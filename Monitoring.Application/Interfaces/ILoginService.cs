using System.Collections.Generic;
using System.Threading.Tasks;

namespace Monitoring.Application.Interfaces
{
    public interface ILoginService
    {
        Task<List<string>> GetAllUsersAsync();
        Task<List<string>> FilterUsersAsync(string query);
        Task<(int? divisionId, bool isValid)> CheckUserCredentialsAsync(string selectedUser, string password);
        // можно добавить остальные методы
    }
}