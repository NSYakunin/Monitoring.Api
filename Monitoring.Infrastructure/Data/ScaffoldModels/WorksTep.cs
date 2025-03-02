using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class WorksTep
{
    public int Id { get; set; }

    public int IdOrderWorksTep { get; set; }

    public DateOnly? DatePlan { get; set; }

    public DateOnly? DateFact { get; set; }

    public double? KolPlan { get; set; }

    public double? KolFact { get; set; }

    public int Month { get; set; }
}
