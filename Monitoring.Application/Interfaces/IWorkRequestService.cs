using Monitoring.Domain.Entities;

namespace Monitoring.Infrastructure.Services
{
    public interface IWorkRequestService
    {

        Task<int> CreateRequestAsync(WorkRequest request);

        Task<List<WorkRequest>> GetRequestsByDocumentNumberAsync(string docNumber);

        Task<List<WorkRequest>> GetPendingRequestsByReceiverAsync(string receiver);

        Task SetRequestStatusAsync(int requestId, string newStatus);

        Task UpdateRequestAsync(WorkRequest req);

        Task DeleteRequestAsync(int requestId);
    }
}