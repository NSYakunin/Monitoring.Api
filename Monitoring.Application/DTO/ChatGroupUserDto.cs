using System;

namespace Monitoring.Application.DTO
{
    public class ChatGroupUserDto
    {
        public long Id { get; set; }

        public int GroupId { get; set; }

        public int UserId { get; set; }

        public bool IsAdmin { get; set; }

        public virtual ChatGroupDto Group { get; set; } = null!;
    }
}