namespace Monitoring.Api.Controllers
{
    public class LoginResponse
    {
        public string Token { get; set; } = "";
        public string UserName { get; set; } = "";
        public int? DivisionId { get; set; }
    }
}
