namespace Monitoring.Domain.Entities
{
    /// <summary>
    /// DTO для хранения данных об исполнении (План, Факт, %).
    /// </summary>
    public class PerformanceDto
    {
        public int DivisionId { get; set; }
        public string DivisionName { get; set; } = string.Empty;
        public int PlanCount { get; set; }
        public int FactCount { get; set; }
        /// <summary>
        /// Доля выполнения от 0 до 1 (например, 0.25 = 25%).
        /// </summary>
        public decimal Percentage { get; set; }
    }
}