using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class UserAllowedDivision
{
    public int Id { get; set; }

    public int IdUser { get; set; }

    public int IdDivision { get; set; }

    public virtual Division IdDivisionNavigation { get; set; } = null!;

    public virtual User IdUserNavigation { get; set; } = null!;
}
