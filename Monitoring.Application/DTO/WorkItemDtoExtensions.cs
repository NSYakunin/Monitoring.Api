namespace Monitoring.Application.DTO
{
    public static class WorkItemDtoExtensions
    {
        public static Monitoring.Domain.Entities.WorkItem ToDomainEntity(this WorkItemDto dto)
        {
            return new Monitoring.Domain.Entities.WorkItem
            {
                DocumentNumber = dto.DocumentNumber,
                DocumentName = dto.DocumentName,
                WorkName = dto.WorkName,
                Executor = dto.Executor,
                Controller = dto.Controller,
                Approver = dto.Approver,
                PlanDate = string.IsNullOrEmpty(dto.PlanDate.Value.ToString()) ? null : DateTime.Parse(dto.PlanDate.Value.ToShortDateString()),
                Korrect1 = string.IsNullOrEmpty(dto.Korrect1.Value.ToString()) ? null : DateTime.Parse(dto.Korrect1.Value.ToShortTimeString()),
                Korrect2 = string.IsNullOrEmpty(dto.Korrect2.Value.ToString()) ? null : DateTime.Parse(dto.Korrect2.Value.ToShortDateString()),
                Korrect3 = string.IsNullOrEmpty(dto.Korrect3.Value.ToString()) ? null : DateTime.Parse(dto.Korrect3.Value.ToShortDateString()),
                FactDate = string.IsNullOrEmpty(dto.FactDate.Value.ToString()) ? null : DateTime.Parse(dto.FactDate.Value.ToShortDateString())
            };
        }
    }
}