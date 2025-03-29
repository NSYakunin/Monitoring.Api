using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Monitoring.Api.Chat
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatController(IHubContext<ChatHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// POST /api/Chat/SendMessage
        /// {
        ///   "user": "Петя",
        ///   "message": "Всем привет из Swagger!"
        /// }
        /// </summary>
        [HttpPost("SendMessage")]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageDto msg)
        {
            // Рассылаем всем через хаб
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", msg.User, msg.Message);
            return Ok(new { success = true, info = "Message sent to all clients via SignalR." });
        }
    }

    public class ChatMessageDto
    {
        public string User { get; set; }
        public string Message { get; set; }
    }
}