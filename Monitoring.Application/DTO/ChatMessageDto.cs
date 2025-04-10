using System;

namespace Monitoring.Application.DTO
{
    public class ChatMessageDto
    {
        public long Id { get; set; }

        public int FromUserId { get; set; }

        public int? ToUserId { get; set; }

        public int? GroupId { get; set; }

        public string MessageText { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public bool IsDeleted { get; set; }
    }
}