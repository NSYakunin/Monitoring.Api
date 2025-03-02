using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class PrivatePenalty
{
    public long IdPrivatePenalty { get; set; }

    public int FkIdUser { get; set; }

    public DateOnly DatePenalty { get; set; }

    public int FkIdPenalty { get; set; }

    public string? NotePrPenalty { get; set; }
}
