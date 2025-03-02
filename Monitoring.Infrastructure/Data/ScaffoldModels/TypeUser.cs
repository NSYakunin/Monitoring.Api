using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class TypeUser
{
    public int Id { get; set; }

    public string Type { get; set; } = null!;
}
