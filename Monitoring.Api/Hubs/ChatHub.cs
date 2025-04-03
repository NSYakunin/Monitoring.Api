// ------------------------------------------------
// ФАЙЛ: Monitoring.Api\Hubs\ChatHub.cs
// ------------------------------------------------
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Monitoring.Application.Interfaces;
using System;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Security.Claims;

namespace Monitoring.Api.Hubs
{
    [Authorize]  // Требуем авторизацию через JWT
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;

        public ChatHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        // Пример: отправка личного сообщения
        public async Task SendPrivateMessage(int toUserId, string message)
        {
            int fromUserId = GetUserIdFromContext();
            var msgDto = await _chatService.SendMessageAsync(fromUserId, toUserId, null, message);

            // Шлём событие "ReceivePrivateMessage" обоим участникам
            await Clients.User(toUserId.ToString())
                .SendAsync("ReceivePrivateMessage", msgDto);
            await Clients.User(fromUserId.ToString())
                .SendAsync("ReceivePrivateMessage", msgDto);
        }

        // Пример: отправка группового сообщения
        public async Task SendGroupMessage(int groupId, string message)
        {
            int fromUserId = GetUserIdFromContext();
            var msgDto = await _chatService.SendMessageAsync(fromUserId, null, groupId, message);

            // Шлём всем в группе (для этого: хранить connectionId в группах, 
            // либо использовать подход "Groups.AddToGroupAsync" в OnConnectedAsync).
            await Clients.Group(groupId.ToString())
                .SendAsync("ReceiveGroupMessage", msgDto);
        }

        public async Task JoinGroup(int groupId)
        {
            // Чтобы подключиться к группе, пользователь должен быть её членом
            // Для простоты прямо сейчас добавим => ChatService.AddUserToGroupAsync (но лучше через контроллер)
            int userId = GetUserIdFromContext();
            await _chatService.AddUserToGroupAsync(groupId, userId);

            await Groups.AddToGroupAsync(Context.ConnectionId, groupId.ToString());
        }

        public async Task LeaveGroup(int groupId)
        {
            int userId = GetUserIdFromContext();
            // В бизнес-логике можно не сразу убирать из GroupUsers, 
            // а просто отключаться от SignalR-группы
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId.ToString());
        }

        private int GetUserIdFromContext()
        {
            var userIdClaim = Context.User?.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null)
                throw new Exception("UserId claim not found");
            return int.Parse(userIdClaim);
        }

        // Пример: добавление пользователя в друзья через хаб
        public async Task AddFriend(int friendUserId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.AddFriendAsync(userId, friendUserId);
            // Можете опционально отправить оповещение по хабу и т.п.
        }

        // Пример: удаление друга
        public async Task RemoveFriend(int friendUserId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.RemoveFriendAsync(userId, friendUserId);
        }

        // Пример: блокировка
        public async Task BlockUser(int blockedUserId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.BlockUserAsync(userId, blockedUserId);
        }

        // Пример: разблокировка
        public async Task UnblockUser(int blockedUserId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.UnblockUserAsync(userId, blockedUserId);
        }

        // Пример: удаление сообщения
        public async Task DeleteMessage(long messageId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.DeleteMessageAsync(messageId, userId);
        }

        // Пример: очистка группового чата
        public async Task ClearGroupHistory(int groupId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.ClearGroupHistoryAsync(groupId, userId);
        }

        // Пример: создание группы
        public async Task<int> CreateGroup(string groupName)
        {
            int userId = GetUserIdFromContext();
            var newGroupId = await _chatService.CreateGroupAsync(userId, groupName);
            return newGroupId;
        }
    }
}