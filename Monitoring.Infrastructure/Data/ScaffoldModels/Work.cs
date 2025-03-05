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

    public DateTime DatePlan { get; set; }

    public int IdDivIsp { get; set; }

    public DateTime? DateKorrect1 { get; set; }

    public DateTime? DateKorrect2 { get; set; }

    public DateTime? DateKorrect3 { get; set; }

    public DateTime? DateFact { get; set; }

    public string? Notes { get; set; }

    public double Importens { get; set; }

    public string? DaysRefresh { get; set; }
}
