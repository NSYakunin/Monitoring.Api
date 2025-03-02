using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class Request
{
    public int Id { get; set; }

    public string WorkDocumentNumber { get; set; } = null!;

    public string DocumentName { get; set; } = null!;

    public string? WorkName { get; set; }

    public string RequestType { get; set; } = null!;

    public string Sender { get; set; } = null!;

    public string Receiver { get; set; } = null!;

    public DateTime RequestDate { get; set; }

    public bool IsDone { get; set; }

    public string? Note { get; set; }

    public DateTime? ProposedDate { get; set; }

    public string Status { get; set; } = null!;

    public string? Executor { get; set; }

    public string? Controller { get; set; }

    public DateTime? PlanDate { get; set; }

    public DateTime? Korrect1 { get; set; }

    public DateTime? Korrect2 { get; set; }

    public DateTime? Korrect3 { get; set; }
}
