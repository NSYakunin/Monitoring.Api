// Monitoring.Infrastructure.Services.WorkRequestAppService

using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities;
using Monitoring.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Monitoring.Infrastructure.Data.ScaffoldModels;

namespace Monitoring.Infrastructure.Services
{
    public class WorkRequestAppService : IWorkRequestService
    {
        private readonly MyDbContext _context;

        public WorkRequestAppService(MyDbContext context)
        {
            _context = context;
        }

        public async Task<int> CreateRequestAsync(WorkRequest request)
        {
            var entity = new Request
            {
                WorkDocumentNumber = request.WorkDocumentNumber,
                DocumentName = request.DocumentName,
                WorkName = request.WorkName,
                RequestType = request.RequestType,
                Sender = request.Sender,
                Receiver = request.Receiver,
                RequestDate = request.RequestDate,
                IsDone = request.IsDone,
                Note = request.Note,
                ProposedDate = request.ProposedDate,
                Status = request.Status,
                Executor = request.Executor,
                Controller = request.Controller,
                PlanDate = request.PlanDate,
                Korrect1 = request.Korrect1,
                Korrect2 = request.Korrect2,
                Korrect3 = request.Korrect3
            };

            _context.Requests.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }

        public async Task<List<WorkRequest>> GetRequestsByDocumentNumberAsync(string docNumber)
        {
            var list = await _context.Requests
                .Where(r => r.WorkDocumentNumber == docNumber)
                .ToListAsync();

            return list.Select(r => new WorkRequest
            {
                Id = r.Id,
                WorkDocumentNumber = r.WorkDocumentNumber,
                DocumentName = r.DocumentName,
                WorkName = r.WorkName,
                RequestType = r.RequestType,
                Sender = r.Sender,
                Receiver = r.Receiver,
                RequestDate = r.RequestDate,
                IsDone = r.IsDone,
                Note = r.Note,
                ProposedDate = r.ProposedDate,
                Status = r.Status,
                Executor = r.Executor,
                Controller = r.Controller,
                PlanDate = r.PlanDate,
                Korrect1 = r.Korrect1,
                Korrect2 = r.Korrect2,
                Korrect3 = r.Korrect3
            }).ToList();
        }

        public async Task<List<WorkRequest>> GetPendingRequestsByReceiverAsync(string receiver)
        {
            var list = await _context.Requests
                .Where(r => r.Receiver == receiver
                            && r.Status == "Pending"
                            && !r.IsDone)
                .ToListAsync();

            return list.Select(r => new WorkRequest
            {
                Id = r.Id,
                WorkDocumentNumber = r.WorkDocumentNumber,
                DocumentName = r.DocumentName,
                WorkName = r.WorkName,
                RequestType = r.RequestType,
                Sender = r.Sender,
                Receiver = r.Receiver,
                RequestDate = r.RequestDate,
                IsDone = r.IsDone,
                Note = r.Note,
                ProposedDate = r.ProposedDate,
                Status = r.Status,
                Executor = r.Executor,
                Controller = r.Controller,
                PlanDate = r.PlanDate,
                Korrect1 = r.Korrect1,
                Korrect2 = r.Korrect2,
                Korrect3 = r.Korrect3
            }).ToList();
        }

        public async Task SetRequestStatusAsync(int requestId, string newStatus)
        {
            var req = await _context.Requests.FirstOrDefaultAsync(r => r.Id == requestId);
            if (req == null) return;

            if (newStatus == "Accepted")
            {
                // Логика обновления WorkUser, как в Razor:
                // Парсим idWork из WorkDocumentNumber (после слэша)
                if (!string.IsNullOrEmpty(req.WorkDocumentNumber) &&
                    req.WorkDocumentNumber.Contains("/"))
                {
                    var parts = req.WorkDocumentNumber.Split('/');
                    if (int.TryParse(parts[^1], out int workId))
                    {
                        // находим userId (sender)
                        var senderUserId = await _context.Users
                            .Where(u => u.SmallName == req.Sender)
                            .Select(u => u.IdUser)
                            .FirstOrDefaultAsync();

                        if (senderUserId > 0 && req.ProposedDate.HasValue)
                        {
                            var wu = await _context.WorkUsers
                                .FirstOrDefaultAsync(w => w.IdWork == workId && w.IdUser == senderUserId);
                            if (wu != null)
                            {
                                switch (req.RequestType?.ToLower())
                                {
                                    case "факт":
                                        wu.DateFact = req.ProposedDate.Value;
                                        break;
                                    case "корр1":
                                        wu.DateKorrect1 = req.ProposedDate.Value;
                                        break;
                                    case "корр2":
                                        wu.DateKorrect2 = req.ProposedDate.Value;
                                        break;
                                    case "корр3":
                                        wu.DateKorrect3 = req.ProposedDate.Value;
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            req.Status = newStatus;
            req.IsDone = true;
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRequestAsync(WorkRequest req)
        {
            var entity = await _context.Requests.FirstOrDefaultAsync(r => r.Id == req.Id);
            if (entity == null) return;
            if (entity.Status != "Pending") return; // уже обработана

            entity.RequestType = req.RequestType;
            entity.Receiver = req.Receiver;
            entity.ProposedDate = req.ProposedDate;
            entity.Note = req.Note;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteRequestAsync(int requestId)
        {
            var entity = await _context.Requests.FirstOrDefaultAsync(r => r.Id == requestId);
            if (entity == null) return;
            if (entity.Status != "Pending") return; // уже обработана

            _context.Requests.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}