using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class WorkUser
{
    public int Id { get; set; }

    public int IdUser { get; set; }

    public int IdWork { get; set; }

    public DateTime? DateFact { get; set; }

    public DateTime? DateKorrect1 { get; set; }

    public DateTime? DateKorrect2 { get; set; }

    public DateTime? DateKorrect3 { get; set; }

    public bool? IsSendToUser { get; set; }
}
