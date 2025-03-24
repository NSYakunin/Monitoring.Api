using Monitoring.Application.DTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Интерфейс для подсветки (Highlight) записей WorkItemDto, 
    /// если есть Pending-заявки от текущего пользователя.
    /// </summary>
    public interface IWorkItemHighlighter
    {
        Task HighlightRowsAsync(List<WorkItemDto> items, string currentUserName);
    }
}