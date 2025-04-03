using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class ChatUserRelationship
{
    public long Id { get; set; }

    public int UserId { get; set; }

    public int OtherUserId { get; set; }

    public bool IsFriend { get; set; }

    public bool IsBlocked { get; set; }
}
