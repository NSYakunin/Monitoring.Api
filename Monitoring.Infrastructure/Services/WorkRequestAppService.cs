using Monitoring.Application.Interfaces;
using Monitoring.Domain.Entities; // Или DTO
using Monitoring.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;
using Monitoring.Infrastructure.Data.ScaffoldModels;

namespace Monitoring.Infrastructure.Services
{
    /// <summary>
    /// Работа с таблицей Requests (заявки).
    /// </summary>
    public class WorkRequestAppService : IWorkRequestService
    {
        private readonly MyDbContext _context;

        public WorkRequestAppService(MyDbContext context)
        {
            _context = context;
        }

        public async Task<int> CreateRequestAsync(WorkRequest request)
        {
            // Допустим, у нас есть EF-сущность Request (из ScaffoldModels).
            // Переносим поля из WorkRequest (Domain) в Request (EF).
            var entity = new Request
            {
                WorkDocumentNumber = request.WorkDocumentNumber,
                DocumentName = request.DocumentName,
                WorkName = request.WorkName,
                RequestType = request.RequestType,
                Sender = request.Sender,
                Receiver = request.Receiver,
                RequestDate = DateTime.Now,
                IsDone = false,
                Note = request.Note,
                ProposedDate = request.ProposedDate,
                Status = "Pending",
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

            // Мапим в WorkRequest
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
                IsDone = r.IsDone ? true : false,
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
                .Where(r =>
                    r.Receiver == receiver
                    && r.Status == "Pending"
                    && (r.IsDone == false || r.IsDone == null)
                )
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
                IsDone = r.IsDone ? true : false,
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
            // Найдём заявку:
            var req = await _context.Requests.FirstOrDefaultAsync(r => r.Id == requestId);
            if (req == null) return;

            // Если "Accepted", нужно поправить WorkUser (dateFact, dateKorrect1/2/3) в зависимости от типа
            if (newStatus == "Accepted")
            {
                // По типу заявки (req.RequestType) определяем, какую дату обновлять
                // Идёт поиск idWork = число после слэша в WorkDocumentNumber
                // И поиск userId = sender, ... (как в вашем коде)
                // Здесь для краткости пропущу подробности, но вы можете сделать по аналогии:
                if (!string.IsNullOrEmpty(req.WorkDocumentNumber) && req.WorkDocumentNumber.Contains("/"))
                {
                    var parts = req.WorkDocumentNumber.Split('/');
                    if (int.TryParse(parts[^1], out int workId))
                    {
                        // найдём userId
                        var userId = await _context.Users
                            .Where(u => u.SmallName == req.Sender)
                            .Select(u => u.IdUser)
                            .FirstOrDefaultAsync();

                        if (userId > 0 && req.ProposedDate.HasValue)
                        {
                            var wu = await _context.WorkUsers
                                .FirstOrDefaultAsync(x => x.IdWork == workId && x.IdUser == userId);
                            if (wu != null)
                            {
                                switch ((req.RequestType ?? "").ToLower())
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

            // Обновляем статус заявки
            req.Status = newStatus;
            req.IsDone = true;
            await _context.SaveChangesAsync();
        }

        public async Task UpdateRequestAsync(WorkRequest req)
        {
            var entity = await _context.Requests.FirstOrDefaultAsync(r => r.Id == req.Id);
            if (entity == null) return;

            if (entity.Status != "Pending") return; // уже нельзя трогать

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
            if (entity.Status != "Pending") return; // нельзя удалить, если уже обработана

            _context.Requests.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}