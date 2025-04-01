using Microsoft.AspNetCore.SignalR;

namespace Monitoring.Api.Chat
{
    /// <summary>
    /// Класс хаба для чата.
    /// </summary>
    public class ChatHub : Hub
    {
        /// <summary>
        /// Отправка текстового сообщения.
        /// </summary>
        public async Task SendMessage(string user, string message)
        {
            // Рассылаем всем событие ReceiveMessage
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        /// <summary>
        /// Отправка файла (в Base64) всем клиентам.
        /// </summary>
        /// <param name="user">Имя пользователя</param>
        /// <param name="fileName">Имя файла (например, image.png)</param>
        /// <param name="fileType">Тип (MIME), например image/png</param>
        /// <param name="base64Data">Содержимое файла в Base64</param>
        public async Task SendFile(string user, string fileName, string fileType, string base64Data)
        {
            // Рассылаем событие ReceiveFile
            await Clients.All.SendAsync("ReceiveFile", user, fileName, fileType, base64Data);
        }
    }
}