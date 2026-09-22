using System;
using System.Collections.Generic;

namespace MuseumAdmin.Models
{
    /// <summary>
    /// Represents an assignment (module/exercise) linked to a child contact
    /// </summary>
    public class ChildAssignmentDto
    {
        public int AssignmentId { get; set; }
        public int ContactId { get; set; }
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = "";
        public string? ExerciseName { get; set; }
        public DateTime AssignedDate { get; set; }
        public string AssignmentStatus { get; set; } = "Active"; // Active, Completed, Paused
        public double? AdherencePercentage { get; set; }
    }

    /// <summary>
    /// Extended contact model with assignment status for Children page
    /// </summary>
    public class ContactWithStatusDto : ContactListDto
    {
        public List<int> ProviderIds { get; set; } = new();

        public string? AssignedProviderName { get; set; }

        /// <summary>
        /// Contact status: Active (has assignments), New (no assignments), Inactive (no longer associated)
        /// </summary>
        public string Status { get; set; } = "New";

        /// <summary>
        /// List of assignments for this contact
        /// </summary>
        public List<AssignedTherapyDto> Assignments { get; set; } = new();

        /// <summary>
        /// Total number of active assignments (excluding expired)
        /// </summary>
        public int AssignmentCount
        {
            get
            {
                if (Assignments == null || !Assignments.Any()) return 0;

                var active = Assignments.Where(a => a.ExpirationDateUtc == null || a.ExpirationDateUtc > DateTime.UtcNow).ToList();
                if (!active.Any()) return 0;

                // Count actual modules (ModuleId > 0)
                int moduleCount = active.Count(a => a.ModuleId > 0);
                if (moduleCount > 0) return moduleCount;

                // If only standalone check-in form(s) assigned without modules, count those
                return active.Count;
            }
        }

        /// <summary>
        /// Average adherence across all assignments
        /// </summary>
        public double? AverageAdherence
        {
            get
            {
                // Current API doesn't provide adherence per therapy, so we return a default or null
                return null;
            }
        }

        /// <summary>
        /// Average adherence across all assignments (as percentage)
        /// </summary>
        public double AdherenceRate { get; set; } = 0; // Set manually if data available

        /// <summary>
        /// Change in adherence from previous period
        /// </summary>
        public double AdherenceChange { get; set; }

        /// <summary>
        /// Total number of active programs/assignments
        /// </summary>
        public int ProgramCount => AssignmentCount;

        /// <summary>
        /// Descriptive status of programs (e.g., "Active", "Paused")
        /// </summary>
        public string ProgramStatus => Status == "Active" ? "Active" : "None";

        /// <summary>
        /// Number of alerts/items needing attention
        /// </summary>
        public int AlertCount { get; set; }

        /// <summary>
        /// Last activity timestamp
        /// </summary>
        public DateTime? LastActivity { get; set; }

        /// <summary>
        /// Flag to track if assignments are currently being fetched in the background
        /// </summary>
        public bool IsLoadingAssignments { get; set; } = false;

        /// <summary>
        /// Calculated age from DOB
        /// </summary>
        public int? CalculatedAge
        {
            get
            {
                if (string.IsNullOrEmpty(Age))
                    return null;

                if (int.TryParse(Age, out int age))
                    return age;

                return null;
            }
        }
    }

    /// <summary>
    /// Request model for assigning a module/exercise to a contact
    /// </summary>
    public class AssignExerciseRequestDto
    {
        public int ContactId { get; set; }
        public int ModuleId { get; set; }
        public string? Notes { get; set; }
        public TimingSettings? Timing { get; set; }
    }

    public class TimingSettings
    {
        public string TimingType { get; set; } = "Relative"; // Relative, Specific
        public int RelativeValue { get; set; } = 1;
        public string RelativeUnit { get; set; } = "Hours";
        public string AfterReference { get; set; } = "After Last Activity";
        public string CompletionType { get; set; } = "Once"; // Once, Daily, Weekly, Custom
        public List<string> RecurrenceDays { get; set; } = new(); // Monday, Tuesday, etc.
        public List<int> SelectedCaregiverIds { get; set; } = new();
        public DateTime? SpecificDateTime { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public bool MoveToArchive { get; set; } = false;
        public bool BlockReflections { get; set; } = false;
        public string NotificationTime { get; set; } = "None";
        public string AudienceMode { get; set; } = "any";
    }

    /// <summary>
    /// Simplified module info for assignment selection
    /// </summary>
    public class ModuleSelectionDto
    {
        public int ModuleId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public List<string> AgeValues { get; set; } = new();
        public List<string> ZoneNames { get; set; } = new();
        public int ExerciseCount { get; set; }
        public List<MuseumAdmin.Components.Pages.Connect.ModuleMessagesTesting> Messages { get; set; } = new();
    }

    // --- New models for AssignTherapy API ---

    public class AssignTherapyRequest
    {
        public TherapySource source { get; set; } = new();
        public int moduleId { get; set; }
        public List<TherapyAssignment> assignments { get; set; } = new();
    }

    public class TherapySource
    {
        public int userId { get; set; }
        public int familyUserId { get; set; }
    }

    public class TherapyAssignment
    {
        public int contactId { get; set; }
        public TherapySchedule? moduleSchedule { get; set; }
        public List<ExerciseSchedule> exerciseSchedules { get; set; } = new();
        public List<int>? exerciseIds { get; set; }
    }

    public class ExerciseSchedule
    {
        public int exerciseId { get; set; }
        public TherapySchedule? schedule { get; set; }
    }

    public class TherapySchedule
    {
        public string type { get; set; } = "string"; // Relative, Specific
        public RelativeTiming? relativeTiming { get; set; }
        public DateTime? specificDateTimeUtc { get; set; }
        public CompletionRule? completionRule { get; set; }
        public string? notificationTime { get; set; }
        public object? assignedTo { get; set; }
    }

    public class RelativeTiming
    {
        public int? value { get; set; }
        public string? unit { get; set; } = "string"; // Minutes, Hours, Days
        public string? relativeTo { get; set; } = "string"; // After Last Activity
    }

    public class CompletionRule
    {
        public string? type { get; set; } = "string"; // Once, Daily
        public DateTime? expirationDateUtc { get; set; }
    }

    // --- New models for GetAllAssignedByChild API ---

    public class AssignedTherapyDto
    {
        public int ModuleAssignmentId { get; set; }
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = "";
        public int? MuseumId { get; set; }
        public DateTime? CreatedUtc { get; set; }
        public string? ScheduleType { get; set; } = "";
        public int? RelativeValue { get; set; }
        public string? RelativeUnit { get; set; }
        public string? RelativeTo { get; set; }
        public DateTime? SpecificDateTimeUtc { get; set; }
        public DateTime? SpecificDateTime { get; set; }
        public string? CompletionType { get; set; } = "";
        public DateTime? ExpirationDateUtc { get; set; }
        public string? NotificationTime { get; set; }
        public object? AssignedTo { get; set; }
        public string? SourceType { get; set; } // Individual, Family, Group
        public string? SourceName { get; set; }
        public int? ProgramId { get; set; }
        public string? ProgramName { get; set; }
        public DateTime? ProgramStartDate { get; set; }
        public DateTime? ProgramEndDate { get; set; }
        public string? ModuleAssignType { get; set; }
        public int? CaregiverUserId { get; set; }
        public bool? IsExpired { get; set; }
        public bool? IsExpiring { get; set; }
        public bool? Archived { get; set; }
        public bool? IsCompleted { get; set; }
        public List<AssignedExerciseDto> Exercises { get; set; } = new();

        // Check-in forms assigned within this module/program entry (added by backend, previously unparsed).
        public List<AssignedProgramFormDto> Forms { get; set; } = new();
    }

    /// <summary>
    /// Represents a check-in form entry nested under a module assignment's "forms" array
    /// (returned by GetAllAssignedByChild), distinct from the lightweight AssignedFormDto
    /// used by TherapistProgramDto.
    /// </summary>
    public class AssignedProgramFormDto
    {
        public string FormId { get; set; } = "";
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? RemindMe { get; set; }
        public string? NotificationTime { get; set; }
        public string? ScheduleType { get; set; }
        public DateTime? SpecificDateTimeUtc { get; set; }
        public string? CompletionType { get; set; }
        public DateTime? ExpirationDateUtc { get; set; }
        public object? AssignedTo { get; set; }
        public object? Questions { get; set; }
    }

    public class AssignedExerciseDto
    {
        public int ExerciseId { get; set; }
        public string? FormId { get; set; }
        public bool? IsCheckInForm { get; set; }
        public string WidgetTitle { get; set; } = "";
        public string WidgetMessage { get; set; } = "";
        public string? ImagePreviewUrl { get; set; }
        public string? VideoUrl { get; set; }
        public string? VideoPreviewUrl { get; set; }
        public string ResponseType { get; set; } = "";
        public string? ExerciseResponseType { get; set; }
        public int? Reward { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? RepeatMode { get; set; } = "";
        public int? NumericalValue { get; set; }
        public int? AssignedBy { get; set; }
        public string? AssignedByName { get; set; }
        public string? ScheduleType { get; set; }
        public int? RelativeValue { get; set; }
        public string? RelativeUnit { get; set; }
        public string? RelativeTo { get; set; }
        public DateTime? SpecificDateTimeUtc { get; set; }
        public DateTime? SpecificDateTime { get; set; }
        public string? CompletionType { get; set; }
        public DateTime? ExpirationDateUtc { get; set; }
        public string? NotificationTime { get; set; }
        public object? AssignedTo { get; set; }
        public TherapySchedule? Schedule { get; set; }
        public int? ProgramId { get; set; }
        public string? ModuleAssignType { get; set; }
        public string? Frequency { get; set; }
        public string? ExerciseTitle { get; set; }
        public bool? Archived { get; set; }
        public string? Recommended { get; set; }
        public bool? IsCompleted { get; set; }
        public bool? IsExpired { get; set; }
        public bool? IsExpiring { get; set; }
        public string? RemindMe { get; set; }
        public string? Timing { get; set; }
        public string? WhenToComplete { get; set; }
        public bool? IsViewed { get; set; }
        public DateTime? ExerciseCompletionDate { get; set; }
        public string? WhentoShow { get; set; }
        public string? ExerciseImageURL { get; set; }
        public int? DurationMinutes { get; set; }
        public int? TimerValue { get; set; }
        public string? Pillar { get; set; }
        public string? Duration { get; set; }
        public string? PlayStyle { get; set; }
        public string? BrainSkills { get; set; }
        public string? ExerciseTime { get; set; }
        public string? TimeRemaining { get; set; }
        public string? CompletionReward { get; set; }
        public object? Questions { get; set; }
    }

    public class CompletedModuleDto
    {
        public int ModuleId { get; set; }
        public string ModuleName { get; set; } = "";
        public bool IsModuleCompleted { get; set; }
        public List<CompletedModuleMessageDto> Messages { get; set; } = new();
    }

    public class CompletedModuleMessageDto
    {
        public int AutoId { get; set; }
        public int ModuleId { get; set; }
        public string WidgetTitle { get; set; } = "";
        public string ResponseType { get; set; } = "";
        public string WidgetMessage { get; set; } = "";
        public bool IsCompletedForContact { get; set; }
        public string? ResponseValue { get; set; }
        public DateTime? ResponseDate { get; set; }
        public List<CompletedModuleMessageImageDto> MessageImages { get; set; } = new();
        public string? AudioURL { get; set; }
    }

    public class CompletedModuleMessageImageDto
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public string MessageImageUrl { get; set; } = "";
        public string? MessageText { get; set; }
        public int MessageOrder { get; set; }
        public int? MuseumId { get; set; }
        public bool? MuseumApproved { get; set; }
        public bool? SubmittedToMuseum { get; set; }
    }

    public class TherapistProgramDto
    {
        public string? CreatedDateTime { get; set; }
        public string? UpdatedDateTime { get; set; }
        public int Id { get; set; }
        public int MuseumId { get; set; }
        public int TherapistId { get; set; }
        public int ContactId { get; set; }
        public int ModuleId { get; set; }
        public string ProgramName { get; set; } = "";
        public DateTime ProgramStartDate { get; set; }
        public int ProgramLengthWeeks { get; set; }
        public DateTime? ProgramExpiryDate { get; set; }
        public string? PrimaryDxCode { get; set; }
        public string? PrimaryDxDesc { get; set; }
        public string? SecondaryDxCode { get; set; }
        public string? SecondaryDxDesc { get; set; }
        public string? Status { get; set; }
        public string? CheckInFormId { get; set; }
        public List<AssignedFormDto> AssignedForms { get; set; } = new();
        public string? ValidationTool { get; set; }
    }

    public class AssignedFormDto
    {
        public string FormId { get; set; } = "";
        public string FormName { get; set; } = "";
    }
}
