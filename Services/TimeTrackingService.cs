using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MuseumAdmin.Data;
using MuseumAdmin.Models;
using Microsoft.Extensions.Logging;


namespace MuseumAdmin.Services
{
    public interface ITimeTrackingService
    {
        Task<TherapistTimeLog> LogTimeAsync(int contactId, string therapistId, int durationMinutes, string activityType, string? note = null, bool isRetroactive = false);
        Task<List<TherapistTimeLog>> GetLogsForMonthAsync(int contactId, int month, int year);
        string GetCptCodeForTime(int totalMinutes);
        Task<bool> LinkLogsToNoteAsync(int noteId, List<int> logIds);
    }

    public class TimeTrackingService : ITimeTrackingService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IAuditService _auditService;
        private readonly ILogger<TimeTrackingService> _logger;

        public TimeTrackingService(IDbContextFactory<ApplicationDbContext> contextFactory, IAuditService auditService, ILogger<TimeTrackingService> logger)
        {
            _contextFactory = contextFactory;
            _auditService = auditService;
            _logger = logger;
        }

        public async Task<TherapistTimeLog> LogTimeAsync(int contactId, string therapistId, int durationMinutes, string activityType, string? note = null, bool isRetroactive = false)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var totalMinutesThisMonth = await context.TherapistTimeLogs
                .Where(l => l.ContactId == contactId && l.StartTime.Month == DateTime.UtcNow.Month && l.StartTime.Year == DateTime.UtcNow.Year)
                .SumAsync(l => l.DurationMinutes);

            var cptCode = GetCptCodeForTime(totalMinutesThisMonth + durationMinutes);

            var log = new TherapistTimeLog
            {
                ContactId = contactId,
                TherapistId = therapistId,
                DurationMinutes = durationMinutes,
                StartTime = DateTime.UtcNow.AddMinutes(-durationMinutes),
                EndTime = DateTime.UtcNow,
                ActivityType = activityType,
                CptCode = cptCode,
                IsRetroactive = isRetroactive,
                AdjustmentJustification = isRetroactive ? note : null,
                CreatedAt = DateTime.UtcNow
            };

            context.TherapistTimeLogs.Add(log);
            await context.SaveChangesAsync();

            await _auditService.LogActionAsync(therapistId, "WRITE", $"Logged {durationMinutes}m time", "TimeLog", log.Id.ToString(), $"CPT: {cptCode}, Retro: {isRetroactive}");

            return log;
        }

        public async Task<List<TherapistTimeLog>> GetLogsForMonthAsync(int contactId, int month, int year)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.TherapistTimeLogs
                .Where(l => l.ContactId == contactId && l.StartTime.Month == month && l.StartTime.Year == year)
                .OrderByDescending(l => l.StartTime)
                .ToListAsync();
        }

        public string GetCptCodeForTime(int totalMinutes)
        {
            // RTM Billing Logic:
            // 0-9 min: Non-billable
            // 10-19 min: 98979
            // 20-39 min: 98980
            // 40+ min: 98980 + 98981 (per 20m)
            
            if (totalMinutes < 10) return "Non-billable";
            if (totalMinutes < 20) return "98979";
            if (totalMinutes < 40) return "98980";
            return "98980 + 98981";
        }

        public async Task<bool> LinkLogsToNoteAsync(int noteId, List<int> logIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var logs = await context.TherapistTimeLogs
                .Where(l => logIds.Contains(l.Id))
                .ToListAsync();

            foreach (var log in logs)
            {
                log.ClinicalNoteId = noteId;
            }

            await context.SaveChangesAsync();
            return true;
        }
    }
}
