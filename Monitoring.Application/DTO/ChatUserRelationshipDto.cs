using System;

namespace Monitoring.Application.DTO
{
    public class ChatUserRelationshipDto
    {
        public long Id { get; set; }

        public int UserId { get; set; }

        public int OtherUserId { get; set; }

        public bool IsFriend { get; set; }

        public bool IsBlocked { get; set; }
    }
}