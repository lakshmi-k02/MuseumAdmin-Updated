namespace MuseumAdmin.Models
{
    public class ContactListDto
    {
        public int ContactId { get; set; }
        public string ContactName { get; set; } = "";
        public string? ContactEmail { get; set; }
        public string ContactPhone { get; set; } = "";
        public string? ContactGender { get; set; }
        public string? Dob { get; set; }
        public int UserId { get; set; }
        public string? ContactProfileImage { get; set; }
        public string? ContactAddedDatetime { get; set; }
        public string? RelationKind { get; set; }
        public string? Age { get; set; }
        public string? ResearchId { get; set; }
        public string? EnrollmentYear { get; set; }
        public string OwnerName { get; set; } = "";
    }
}
