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
    public interface IClinicalNoteService
    {
        Task<ClinicalNote?> GetNoteAsync(int contactId, string therapistId);
        Task<List<ClinicalNoteVersion>> GetNoteHistoryAsync(int noteId);
        Task<ClinicalNote> SaveNoteAsync(int contactId, string therapistId, ClinicalNoteVersion versionData, string? amendmentReason = null);
        Task<bool> SignNoteAsync(int noteId, string operatorName);
        Task<bool> HasPriorSignedSetupNoteAsync(int contactId);
        Task<List<ClinicalNote>> GetSignedNotesForChildAsync(int contactId);
    }

    public class ClinicalNoteService : IClinicalNoteService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IAuditService _auditService;
        private readonly ILogger<ClinicalNoteService> _logger;

        public ClinicalNoteService(IDbContextFactory<ApplicationDbContext> contextFactory, IAuditService auditService, ILogger<ClinicalNoteService> logger)
        {
            _contextFactory = contextFactory;
            _auditService = auditService;
            _logger = logger;
        }

        // In-memory mock storage for prototyping
        private static readonly List<ClinicalNote> _mockNotes = new();
        private static readonly List<ClinicalNoteVersion> _mockVersions = new();

        public async Task<ClinicalNote?> GetNoteAsync(int contactId, string therapistId)
        {
            await Task.Delay(50); // Simulate network
            return _mockNotes.FirstOrDefault(n => n.ContactId == contactId && n.TherapistId == therapistId);
        }

        public async Task<List<ClinicalNoteVersion>> GetNoteHistoryAsync(int noteId)
        {
            await Task.Delay(50);
            return _mockVersions
                .Where(v => v.ClinicalNoteId == noteId)
                .OrderByDescending(v => v.VersionNumber)
                .ToList();
        }

        public async Task<ClinicalNote> SaveNoteAsync(int contactId, string therapistId, ClinicalNoteVersion versionData, string? amendmentReason = null)
        {
            await Task.Delay(100);
            
            var note = _mockNotes.FirstOrDefault(n => n.ContactId == contactId && n.TherapistId == therapistId);

            if (note == null)
            {
                note = new ClinicalNote
                {
                    Id = _mockNotes.Count + 1,
                    ContactId = contactId,
                    TherapistId = therapistId,
                    CreatedAt = DateTime.UtcNow,
                    Status = ClinicalNoteStatus.Draft
                };
                _mockNotes.Add(note);
            }

            if (note.Status == ClinicalNoteStatus.Signed || note.Status == ClinicalNoteStatus.Amended)
            {
                note.Status = ClinicalNoteStatus.Amended;
                versionData.AmendmentReason = amendmentReason ?? "Clinical update";
            }

            var nextVersionNumber = (_mockVersions.Where(v => v.ClinicalNoteId == note.Id).Any() 
                ? _mockVersions.Where(v => v.ClinicalNoteId == note.Id).Max(v => v.VersionNumber) 
                : 0) + 1;
            
            var newVersion = new ClinicalNoteVersion
            {
                Id = _mockVersions.Count + 1,
                ClinicalNoteId = note.Id,
                VersionNumber = nextVersionNumber,
                Subjective = versionData.Subjective,
                Objective = versionData.Objective,
                Assessment = versionData.Assessment,
                Plan = versionData.Plan,
                InteractiveCommunicationCompleted = versionData.InteractiveCommunicationCompleted,
                ClinicalMinutes = versionData.ClinicalMinutes,
                AppDaysCount = versionData.AppDaysCount,
                AdherenceRate = versionData.AdherenceRate,
                SubjectiveDone = versionData.SubjectiveDone,
                ObjectiveDone = versionData.ObjectiveDone,
                AssessmentDone = versionData.AssessmentDone,
                PlanDone = versionData.PlanDone,
                AuthorId = therapistId,
                Timestamp = DateTime.UtcNow,
                IsFinal = false,
                AmendmentReason = versionData.AmendmentReason
            };

            _mockVersions.Add(newVersion);
            return note;
        }

        public async Task<bool> SignNoteAsync(int noteId, string operatorName)
        {
            await Task.Delay(100);
            var note = _mockNotes.FirstOrDefault(n => n.Id == noteId);

            if (note == null || note.Status == ClinicalNoteStatus.Signed)
            {
                return false;
            }

            note.Status = ClinicalNoteStatus.Signed;
            note.SignedAt = DateTime.UtcNow;
            note.SignedBy = operatorName;

            var latestVersion = _mockVersions
                .Where(v => v.ClinicalNoteId == noteId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();
                
            if (latestVersion != null)
            {
                latestVersion.IsFinal = true;
            }

            return true;
        }

        public async Task<bool> HasPriorSignedSetupNoteAsync(int contactId)
        {
            await Task.Delay(50);
            return _mockNotes.Any(n => n.ContactId == contactId && n.Status == ClinicalNoteStatus.Signed 
                && _mockVersions.Any(v => v.ClinicalNoteId == n.Id && v.IsInitialInteraction && v.IsFinal));
        }

        public async Task<List<ClinicalNote>> GetSignedNotesForChildAsync(int contactId)
        {
            await Task.Delay(50);
            return _mockNotes
                .Where(n => n.ContactId == contactId && (n.Status == ClinicalNoteStatus.Signed || n.Status == ClinicalNoteStatus.Amended))
                .OrderByDescending(n => n.SignedAt ?? n.CreatedAt)
                .ToList();
        }
    }
}
