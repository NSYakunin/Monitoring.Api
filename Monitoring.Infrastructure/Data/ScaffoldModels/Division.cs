using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class Division
{
    public int IdDivision { get; set; }

    public int? IdParentDivision { get; set; }

    public string NameDivision { get; set; } = null!;

    public string? SmallNameDivision { get; set; }

    public int? Position { get; set; }

    public int? IdUserHead { get; set; }

    public virtual ICollection<UserAllowedDivision> UserAllowedDivisions { get; set; } = new List<UserAllowedDivision>();
}
