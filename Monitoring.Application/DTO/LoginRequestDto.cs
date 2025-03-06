namespace Monitoring.Application.DTO
{
    /// <summary>
    /// Модель запроса для логина
    /// </summary>
    public class LoginRequestDto
    {
        public string SelectedUser { get; set; } = "";
        public string Password { get; set; } = "";
    }
}