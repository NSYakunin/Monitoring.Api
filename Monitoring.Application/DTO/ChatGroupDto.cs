using System;
using System.Collections.Generic;

namespace Monitoring.Application.DTO
{
    public class ChatGroupDto
    {
        public int Id { get; set; }

        public string GroupName { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<ChatGroupUserDto> ChatGroupUsers { get; set; } = new List<ChatGroupUserDto>();
    }
}