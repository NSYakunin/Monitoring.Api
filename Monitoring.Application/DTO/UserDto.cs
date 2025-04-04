// ------------------------------
// Monitoring.Application\DTO\UserDto.cs
// ------------------------------
namespace Monitoring.Application.DTO
{
    public class UserDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } // сюда можем класть SmallName или Name
    }
}