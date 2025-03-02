using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class Document
{
    public int Id { get; set; }

    public string? Number { get; set; }

    public int? IdTypeDoc { get; set; }

    public string? Name { get; set; }

    public string? NumDog { get; set; }

    public DateOnly? DateCreate { get; set; }

    public DateOnly? DateSetInSystem { get; set; }

    public int? IdAuthor { get; set; }

    public int? IdUtverUser { get; set; }

    public string? Notes { get; set; }

    public bool? IsActive { get; set; }

    public bool? IsInWork { get; set; }
}
