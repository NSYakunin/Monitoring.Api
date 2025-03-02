using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class Month
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int? Kvartal { get; set; }
}
