namespace Monitoring.Application.DTO
{
    /// <summary>
    /// Модель запроса для логина
    /// </summary>
    public class LoginRequestDto
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}