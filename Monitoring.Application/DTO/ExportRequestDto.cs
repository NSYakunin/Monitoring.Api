using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Application.DTO
{
    public class ExportRequestDto
    {
        public string Format { get; set; } = "pdf";  // "pdf", "excel", "word"
        public List<string>? SelectedItems { get; set; }  // Док.номера, которые выбраны
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Executor { get; set; }
        public string? Approver { get; set; }
        public string? Search { get; set; }
        public int? DivisionId { get; set; }
    }
}
