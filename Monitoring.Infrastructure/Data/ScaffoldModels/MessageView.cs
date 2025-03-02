using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class MessageView
{
    public int Id { get; set; }

    public string? IdDocument { get; set; }

    public string? Name { get; set; }

    public DateOnly? DateSetInSystem { get; set; }

    public int? IdUser { get; set; }

    public bool? IsActive { get; set; }
}
