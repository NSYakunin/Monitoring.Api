using System;
using System.Collections.Generic;

namespace Monitoring.Infrastructure.Data.ScaffoldModels;

public partial class ContractFile
{
    public int FileId { get; set; }

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;
}
