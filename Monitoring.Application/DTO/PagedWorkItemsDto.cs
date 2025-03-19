// PagedWorkItemsDto.cs (можно разместить рядом, в папке DTO)
namespace Monitoring.Application.DTO
{
    public class PagedWorkItemsDto
    {
        public List<WorkItemDto> Items { get; set; } = new();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }
}
