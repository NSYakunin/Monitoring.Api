using Microsoft.AspNetCore.SignalR;

namespace Monitoring.Api.Chat
{
    /// <summary чата.
    /// Любой клиент может вызвать SendMessage(user, message), и все получат событие ReceiveMessage.
    /// </summary>
    public class ChatHub : Hub
    {
        /// <summary>
        /// Клиент (JS или другой) вызывает connection.invoke("SendMessage", user, message).
        /// Мы рассылаем сообщение всем, кто подключён.
        /// </summary>
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
    }
}