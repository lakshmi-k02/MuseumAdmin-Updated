using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MuseumAdmin.Models
{
    public enum ClinicalNoteStatus
    {
        Draft,
        Signed,
        Amended
    }

    public class ClinicalNote
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ContactId { get; set; }

        [Required]
        public string TherapistId { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ClinicalNoteStatus Status { get; set; } = ClinicalNoteStatus.Draft;

        public DateTime? SignedAt { get; set; }

        public string? SignedBy { get; set; }

        public virtual ICollection<ClinicalNoteVersion> Versions { get; set; } = new List<ClinicalNoteVersion>();
        
        public virtual ICollection<TherapistTimeLog> TimeLogs { get; set; } = new List<TherapistTimeLog>();
    }

    public class ClinicalNoteVersion
    {
        [Key]
        public int Id { get; set; }

        public int ClinicalNoteId { get; set; }

        [ForeignKey("ClinicalNoteId")]
        public virtual ClinicalNote? ClinicalNote { get; set; }

        public int VersionNumber { get; set; }

        public string Subjective { get; set; } = "";

        public string Objective { get; set; } = "";

        public string Assessment { get; set; } = "";

        public string Plan { get; set; } = "";

        public bool InteractiveCommunicationCompleted { get; set; }

        public string? CommunicationType { get; set; }

        public int ClinicalMinutes { get; set; }

        public int AppDaysCount { get; set; }

        public int AdherenceRate { get; set; }

        public bool SubjectiveDone { get; set; }
        public bool ObjectiveDone { get; set; }
        public bool AssessmentDone { get; set; }
        public bool PlanDone { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;


        public string AuthorId { get; set; } = "";
        
        public bool IsFinal { get; set; }
        
        public bool IsInitialInteraction { get; set; }

        public string SelectedCptCode { get; set; } = "";

        public string? AmendmentReason { get; set; }
    }

    public class TherapistTimeLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ContactId { get; set; }

        [Required]
        public string TherapistId { get; set; } = "";

        public int? ClinicalNoteId { get; set; }

        [ForeignKey("ClinicalNoteId")]
        public virtual ClinicalNote? ClinicalNote { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public int DurationMinutes { get; set; }

        public string CptCode { get; set; } = "";

        public string ActivityType { get; set; } = "Review"; // Review, Communication, Documentation

        public string? AdjustmentJustification { get; set; }

        public bool IsRetroactive { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string UserId { get; set; } = "";

        public string ActionType { get; set; } = ""; // READ, WRITE, LOGIN, EXPORT

        public string Action { get; set; } = "";

        public string ResourceType { get; set; } = ""; // ClinicalNote, TimeLog, PHI

        public string? ResourceId { get; set; }

        public string Detail { get; set; } = "";

        public string IpAddress { get; set; } = "";

        public bool Success { get; set; } = true;
    }
}
