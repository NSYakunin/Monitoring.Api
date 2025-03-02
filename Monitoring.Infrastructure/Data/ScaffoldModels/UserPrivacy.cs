using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class UserPrivacy
{
    public int IdUser { get; set; }

    public bool CanCloseWork { get; set; }

    public bool CanSendCloseRequest { get; set; }

    public bool CanAccessSettings { get; set; }

    public virtual User IdUserNavigation { get; set; } = null!;
}
