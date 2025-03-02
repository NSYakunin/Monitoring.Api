﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monitoring.Application.DTO
{
    public class WorkItemDto
    {
        public string DocumentNumber { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public string WorkName { get; set; } = string.Empty;

        /// <summary>
        /// Исполнитель. Для простоты - строка (smallName).
        /// </summary>
        public string Executor { get; set; } = string.Empty;

        public string Controller { get; set; } = string.Empty;
        public string Approver { get; set; } = string.Empty;

        public DateOnly? PlanDate { get; set; }
        public DateOnly? Korrect1 { get; set; }
        public DateOnly? Korrect2 { get; set; }
        public DateOnly? Korrect3 { get; set; }
        public DateOnly? FactDate { get; set; }

        public string HighlightCssClass { get; set; } = "";

        // Для хранения Pending-заявки от текущего пользователя
        public int? UserPendingRequestId { get; set; }
        public string? UserPendingRequestType { get; set; }
        public DateOnly? UserPendingProposedDate { get; set; }
        public string? UserPendingRequestNote { get; set; }
        public string? UserPendingReceiver { get; set; }
    }
}
