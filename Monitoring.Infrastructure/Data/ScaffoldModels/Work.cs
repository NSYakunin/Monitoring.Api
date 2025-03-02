using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class Work
{
    public int Id { get; set; }

    public int IdDocuments { get; set; }

    public string? Razdel { get; set; }

    public string? CurrNum { get; set; }

    public string? Rezult { get; set; }

    public string? Name { get; set; }

    public DateOnly DatePlan { get; set; }

    public int IdDivIsp { get; set; }

    public DateOnly? DateKorrect1 { get; set; }

    public DateOnly? DateKorrect2 { get; set; }

    public DateOnly? DateKorrect3 { get; set; }

    public DateOnly? DateFact { get; set; }

    public string? Notes { get; set; }

    public double Importens { get; set; }

    public string? DaysRefresh { get; set; }
}
