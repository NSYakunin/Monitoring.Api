using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class WorkUserCheck
{
    public int Id { get; set; }

    public int IdUser { get; set; }

    public int IdWork { get; set; }

    public bool? IsSendToUser { get; set; }
}
