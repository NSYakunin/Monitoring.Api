using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class OrderWorksTep
{
    public int Id { get; set; }

    public int IdDiv { get; set; }

    public string? Razdel { get; set; }

    public int? Pp { get; set; }

    public string NamePokaz { get; set; } = null!;

    public string TypePokaz { get; set; } = null!;

    public int Year { get; set; }
}
