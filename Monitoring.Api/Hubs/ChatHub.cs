﻿using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Monitoring.Application.Interfaces;
using System;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Security.Claims;
using System.Collections.Generic;
using Monitoring.Application.DTO;

namespace Monitoring.Api.Hubs
{
    [Authorize] // Требуем авторизацию через JWT
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly IUserService _userService;

        public ChatHub(IChatService chatService, IUserService userService)
        {
            _chatService = chatService;
            _userService = userService;
        }

        // ------------------------
        // Методы для приватного чата
        // ------------------------
        public async Task SendPrivateMessage(int toUserId, string message)
        {
            int fromUserId = GetUserIdFromContext();
            var msgDto = await _chatService.SendMessageAsync(fromUserId, toUserId, null, message);

            // Шлём событие и отправителю (Caller), и получателю (User(toUserId))
            await Clients.User(toUserId.ToString())
                .SendAsync("ReceivePrivateMessage", msgDto);
            await Clients.User(fromUserId.ToString())
                .SendAsync("ReceivePrivateMessage", msgDto);
        }

        public async Task<List<ChatMessageDto>> GetPrivateMessages(int friendUserId)
        {
            int currentUserId = GetUserIdFromContext();
            return await _chatService.GetPrivateMessagesAsync(currentUserId, friendUserId);
        }

        // ------------------------
        // Методы для группового чата
        // ------------------------
        public async Task SendGroupMessage(int groupId, string message)
        {
            int fromUserId = GetUserIdFromContext();
            var msgDto = await _chatService.SendMessageAsync(fromUserId, null, groupId, message);

            await Clients.Group(groupId.ToString())
                .SendAsync("ReceiveGroupMessage", msgDto);
        }

        public async Task JoinGroup(int groupId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.AddUserToGroupAsync(groupId, userId);

            await Groups.AddToGroupAsync(Context.ConnectionId, groupId.ToString());
        }

        public async Task LeaveGroup(int groupId)
        {
            int userId = GetUserIdFromContext();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId.ToString());
        }

        // ------------------------
        // Работа с друзьями и блокировкой
        // ------------------------
        public async Task AddFriend(int friendUserId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.AddFriendAsync(userId, friendUserId);
        }

        public async Task RemoveFriend(int friendUserId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.RemoveFriendAsync(userId, friendUserId);
        }

        public async Task BlockUser(int blockedUserId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.BlockUserAsync(userId, blockedUserId);
        }

        public async Task UnblockUser(int blockedUserId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.UnblockUserAsync(userId, blockedUserId);
        }

        // ------------------------
        // GetFriends, GetAllUsersExceptMe
        // ------------------------
        public async Task<List<UserChatDto>> GetFriends()
        {
            int userId = GetUserIdFromContext();
            return await _chatService.GetFriendsAsync(userId);
        }

        public async Task<List<UserDto>> GetAllUsersExceptMe()
        {
            int userId = GetUserIdFromContext();
            return await _userService.GetAllUsersExceptAsync(userId);
        }

        // ------------------------
        // Удаление сообщений, очистка, группы
        // ------------------------
        public async Task DeleteMessage(long messageId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.DeleteMessageAsync(messageId, userId);
        }

        public async Task ClearGroupHistory(int groupId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.ClearGroupHistoryAsync(groupId, userId);
        }

        public async Task<int> CreateGroup(string groupName)
        {
            int userId = GetUserIdFromContext();
            var newGroupId = await _chatService.CreateGroupAsync(userId, groupName);
            return newGroupId;
        }

        public async Task ClearPrivateHistory(int friendUserId)
        {
            int userId = GetUserIdFromContext();
            await _chatService.ClearPrivateHistoryAsync(userId, friendUserId);
        }

        // ------------------------
        // Вспомогательный метод
        // ------------------------
        private int GetUserIdFromContext()
        {
            var userIdClaim = Context.User?.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null)
                throw new Exception("UserId claim not found");
            return int.Parse(userIdClaim);
        }
    }
}