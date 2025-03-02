using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class SpPenalty
{
    public int IdIdPenalty { get; set; }

    public short IdSpPaper { get; set; }

    public string NameP { get; set; } = null!;

    public short SummPDiv { get; set; }

    public short SummPWorker { get; set; }

    public short SummPChief { get; set; }

    public bool IsvalidP { get; set; }
}
