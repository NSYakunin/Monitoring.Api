using Monitoring.Application.DTO;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Сервис для разных настроек пользователя (privacy, доступы, AllowedDivisions, смена пароля и т.д.)
    /// </summary>
    public interface IUserSettingsService
    {
        Task<PrivacySettingsDto> GetPrivacySettingsAsync(int userId);
        Task SavePrivacySettingsAsync(int userId, PrivacySettingsDto dto, bool isActive);

        Task<bool> HasAccessToSettingsAsync(int userId);
        Task<bool> HasAccessToSendCloseRequestAsync(int userId);
        Task<bool> HasAccessToCloseWorkAsync(int userId);

        // Проверка, активен ли пользователь (Isvalid=1)
        Task<bool> IsUserValidAsync(int userId);

        // Список всех подразделений
        Task<List<DivisionDto>> GetAllDivisionsAsync();

        // Список "AllowedDivisions" для пользователя
        Task<List<int>> GetUserAllowedDivisionsAsync(int userId);

        // Сохранить "AllowedDivisions" для пользователя
        Task SaveUserAllowedDivisionsAsync(int userId, List<int> divisionIds);

        // Смена пароля
        Task ChangeUserPasswordAsync(int userId, string newPassword);
        Task<string?> GetUserCurrentPasswordAsync(int userId);

        // Регистрация нового пользователя (пример)
        Task<int> RegisterUserInDbAsync(
            string fullName,
            string smallName,
            string password,
            int? idDivision,
            bool canCloseWork,
            bool canSendCloseRequest,
            bool canAccessSettings
        );
    }
}