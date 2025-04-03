using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class ChatGroupUser
{
    public long Id { get; set; }

    public int GroupId { get; set; }

    public int UserId { get; set; }

    public bool IsAdmin { get; set; }

    public virtual ChatGroup Group { get; set; } = null!;
}
