using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MuseumAdmin.Models
{
    public class AddFamilyDto
    {
        public int MuseumId { get; set; }

        // Primary Parent Details
        [Required(ErrorMessage = "First name is required")]
        public string FirstName { get; set; } = "";

        [Required(ErrorMessage = "Last name is required")]
        public string LastName { get; set; } = "";

        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits")]
        public string PhoneNumber { get; set; } = "";

        [Required(ErrorMessage = "Country code is required")]
        public string CountryCode { get; set; } = "+1";

        public string Email { get; set; } = "";

        public DateTime? Birthday { get; set; }

        // Additional caregiver details (Optional)
        public bool HasPartner { get; set; }
        public PartnerDto? Partner { get; set; }
        public List<PartnerDto> Caregivers { get; set; } = new();

        // Children Details
        public List<ChildDto> Children { get; set; } = new();

        // Context & Permissions
        public string ResearchId { get; set; } = "";
        public int? EnrollmentYear { get; set; } 
        public string PermissionMessage { get; set; } = "";
        
        // Provider Selection (UI Only for now)
        public List<int> ProviderIds { get; set; } = new();
    }

    // Reused for partner and caregiver rows.
    public class PartnerDto
    {
        [JsonPropertyName("partnerId")]
        public int? PartnerId { get; set; }

        [JsonPropertyName("caregiverId")]
        public int? CaregiverId { get; set; }

        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonIgnore]
        public int? EffectiveId => CaregiverId ?? PartnerId ?? Id;

        [JsonPropertyName("isPrimary")]
        public bool? IsPrimary { get; set; }

        [JsonPropertyName("isSecondary")]
        public bool? IsSecondary { get; set; }

        [Required(ErrorMessage = "Partner first name is required")]
        [JsonPropertyName("firstName")]
        public string FirstName { get; set; } = "";

        [Required(ErrorMessage = "Partner last name is required")]
        [JsonPropertyName("lastName")]
        public string LastName { get; set; } = "";

        [Required(ErrorMessage = "Partner phone number is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be exactly 10 digits")]
        [JsonPropertyName("phoneNumber")]
        public string PhoneNumber { get; set; } = "";

        [Required(ErrorMessage = "Country code is required")]
        [JsonPropertyName("countryCode")]
        public string CountryCode { get; set; } = "+1";

        [JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [JsonPropertyName("relationToChild")]
        public string RelationToChild { get; set; } = "";

        [JsonPropertyName("birthday")]
        public DateTime? Birthday { get; set; }
    }

    public class ChildDto
    {
        public int? ContactId { get; set; }
        
        [Required(ErrorMessage = "First name is required")]
        public string FirstName { get; set; } = "";
        [Required(ErrorMessage = "Last name is required")]
        public string LastName { get; set; } = "";
        public DateTime? dob { get; set; }
        
        // Keep UI binding on Gender, but serialize compatibility aliases for backend variants.
        [Required(ErrorMessage = "Gender is required")]
        [JsonPropertyName("gender")]
        public string Gender { get; set; } = "";

        [JsonPropertyName("contactGeneder")]
        public string? ContactGenederCompat
        {
            get => string.IsNullOrWhiteSpace(Gender) ? null : Gender;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Gender = value;
                }
            }
        }

        [JsonPropertyName("contactGender")]
        public string? ContactGenderCompat
        {
            get => string.IsNullOrWhiteSpace(Gender) ? null : Gender;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Gender = value;
                }
            }
        }

        public string ArtTitle { get; set; } = "";
        public string MessageAudioText { get; set; } = "";

        public string RelationToChild { get; set; } = "";
        [Required(ErrorMessage = "Relationship is required")]
        public int? RelationKind { get; set; }

        // Media Upload Handlers (Recommendation)
        // For JSON-based upload, you might use Base64 strings.
        // Alternatively, use a separate endpoint for file uploads.
        public string? ImageBase64 { get; set; }
        public string? AudioBase64 { get; set; }

        // UI-only properties for file name display
        public string ImageFileName { get; set; } = "No file chosen";
        public string AudioFileName { get; set; } = "No file chosen";
    }

    public class AddChildRequestDto
    {
        public int userId { get; set; }
        public ChildDto child { get; set; } = new();
    }

    public class UpdateChildrenRequestDto
    {
        public int userId { get; set; }
        public List<ChildDto> children { get; set; } = new();
    }
}
