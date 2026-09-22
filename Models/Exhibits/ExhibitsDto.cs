using System;

namespace MuseumAdmin.Models.Exhibits
{
    /// <summary>
    /// Exhibit DTO mapped from GetTravellingExhibits API
    /// </summary>
    public class ExhibitDto
    {
        public int Id { get; set; }
        public int MuseumId { get; set; }
        public int LocationId { get; set; }

        public string ExhibitName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IdealAges { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;

        public string Travelling { get; set; } = string.Empty;

        public string LocalDisplayLocationName { get; set; } = string.Empty;
        public string LocalDisplay { get; set; } = string.Empty;

        public string ExhibitCoverImage { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }

        public string Startover { get; set; } = string.Empty;
        public string EndCover { get; set; } = string.Empty;
        public string ReminderCover { get; set; } = string.Empty;

        public int? ReminderInterval { get; set; }
            // 👇 NEW (future-ready)
        public string? Status { get; set; }
        public bool IsSelected { get; set; }
        public string? MuseumCreator { get; set; }
    }
}
