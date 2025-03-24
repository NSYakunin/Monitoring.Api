using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Сервис для подсветки WorkItemDto, если есть Pending-заявки от текущего пользователя 
    /// (ищем в IWorkRequestService).
    /// </summary>
    public class WorkItemHighlighter : IWorkItemHighlighter
    {
        private readonly IWorkRequestService _workRequestService;

        public WorkItemHighlighter(IWorkRequestService workRequestService)
        {
            _workRequestService = workRequestService;
        }

        /// <summary>
        /// Проверяем для каждого WorkItemDto, есть ли Pending-заявка, 
        /// где Sender == currentUserName. Если есть, проставляем класс выделения.
        /// </summary>
        public async Task HighlightRowsAsync(List<WorkItemDto> items, string currentUserName)
        {
            foreach (var item in items)
            {
                var requests = await _workRequestService.GetRequestsByDocumentNumberAsync(item.DocumentNumber);
                var pendingFromMe = requests.FirstOrDefault(r =>
                    r.Status == "Pending"
                    && !r.IsDone
                    && r.Sender.Equals(currentUserName, System.StringComparison.OrdinalIgnoreCase)
                );

                if (pendingFromMe != null)
                {
                    // Если это "факт", красим в table-info. Если начинается с "корр", красим в table-warning
                    if (pendingFromMe.RequestType == "факт")
                        item.HighlightCssClass = "table-info";
                    else if (pendingFromMe.RequestType.StartsWith("корр"))
                        item.HighlightCssClass = "table-warning";

                    item.UserPendingRequestId = pendingFromMe.Id;
                    item.UserPendingRequestType = pendingFromMe.RequestType;
                    item.UserPendingProposedDate = pendingFromMe.ProposedDate;
                    item.UserPendingRequestNote = pendingFromMe.Note;
                    item.UserPendingReceiver = pendingFromMe.Receiver;
                }
            }
        }
    }
}