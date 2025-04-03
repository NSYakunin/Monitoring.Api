using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class ChatGroup
{
    public int Id { get; set; }

    public string GroupName { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<ChatGroupUser> ChatGroupUsers { get; set; } = new List<ChatGroupUser>();
}
