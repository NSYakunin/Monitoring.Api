using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Application.DTO
{
    public class CreateRequestDto
    {
        public string DocumentNumber { get; set; } = string.Empty;
        public string RequestType { get; set; } = string.Empty;
        public DateTime? ProposedDate { get; set; }
        public string? Note { get; set; }
        public string Receiver { get; set; } = string.Empty;
    }
}
