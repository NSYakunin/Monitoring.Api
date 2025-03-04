namespace Monitoring.Api.Controllers
{
    public class LoginRequest
    {
        public string SelectedUser { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
