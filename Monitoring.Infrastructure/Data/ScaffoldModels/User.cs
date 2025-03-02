using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class User
{
    public int IdUser { get; set; }

    public string Name { get; set; } = null!;

    public string? SmallName { get; set; }

    public int? IdDivision { get; set; }

    public string? Password { get; set; }

    public int? IdTypeUser { get; set; }

    public bool Isvalid { get; set; }

    public virtual ICollection<UserAllowedDivision> UserAllowedDivisions { get; set; } = new List<UserAllowedDivision>();

    public virtual UserPrivacy? UserPrivacy { get; set; }
}
