namespace Monitoring.Application.Interfaces
{
    public interface ILoginService
    {
        /// <summary>
        /// Возвращает список активных пользователей (Isvalid=1) – поле smallName.
        /// </summary>
        Task<List<string>> GetAllUsersAsync();

        /// <summary>
        /// Фильтрует пользователей (Isvalid=1, LIKE '%query%').
        /// </summary>
        Task<List<string>> FilterUsersAsync(string query);

        /// <summary>
        /// Проверяет логин/пароль, возвращая (divisionId, isValid).
        /// Если всё ок, isValid=true и divisionId!=null.
        /// </summary>
        Task<(int? userId, int? divisionId, bool isValid)> CheckUserCredentialsAsync(
            string selectedUser,
            string password
        );

        // При необходимости здесь могут быть и другие методы:
        // Task<int?> GetUserIdByNameAsync(string userName);
    }
}