namespace MuseumAdmin.Models
{
    public class MessageDto
    {
        public int messageId { get; set; }

        public string? messageTitle { get; set; }
        public string? messageImage { get; set; }
        public string? messageCreatorName { get; set; }
        public string? messageAudio { get; set; }
        public string? userEmailAddress { get; set; }
        public string? artTitle { get; set; }
        public string? artistName { get; set; }
        public string? artistAge { get; set; }

        public DateTime? createdDate { get; set; }
        public DateTime? updatedDate { get; set; }

        public string? deviceDetails { get; set; }
        public string? musiumName { get; set; }

        // 🔥 IMPORTANT FIX
        public bool? isApproved { get; set; }

        public DateTime? approvedDate { get; set; }
        public string? approvedBy { get; set; }

        public string? countryCode { get; set; }
        public string? countryName { get; set; }
        public string? userPhoneNumber { get; set; }

        public int? musiumid { get; set; }
        public bool? userApproved { get; set; }

        public string? approvedDateString { get; set; }
        public List<string>? tags { get; set; } = new List<string>();
        
        [System.Text.Json.Serialization.JsonPropertyName("isActive")]
        public bool isActive { get; set; }
    }
}

