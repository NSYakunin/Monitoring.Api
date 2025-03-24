using Monitoring.Application.DTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Monitoring.Application.Interfaces
{
    /// <summary>
    /// Репозиторий для работы с WorkItem'ами: получение данных из БД (с кэшированием),
    /// получение названий подразделений, списков исполнителей/принимающих и т.д.
    /// </summary>
    public interface IWorkItemRepository
    {
        /// <summary>
        /// Получить все WorkItemDto для списка divisionIds, 
        /// с объединением дублей (агрегация исполнителей и пр.).
        /// </summary>
        Task<List<WorkItemDto>> GetWorkItemsForDivisionsAsync(List<int> divisionIds);

        /// <summary>
        /// Очистить кэш для указанного divisionId (если нужно).
        /// </summary>
        void ClearCache(int divisionId);

        /// <summary>
        /// Получить название отдела (smallNameDivision) из таблицы Divisions.
        /// </summary>
        Task<string> GetDivisionNameAsync(int divisionId);

        /// <summary>
        /// Список исполнителей (smallName) внутри отдела. Если divisionId=0 - вернуть всех.
        /// </summary>
        Task<List<string>> GetExecutorsByDivisionIdAsync(int divisionId);

        /// <summary>
        /// Список принимающих (smallName). Если divisionId=0 - вернуть всех.
        /// </summary>
        Task<List<string>> GetApproversByDivisionIdAsync(int divisionId);
    }
}