using System;
using System.Text.Json.Serialization;

namespace MuseumAdmin.Models
{
    public class UserListDto
    {
        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        [JsonPropertyName("museumId")]
        public int MuseumId { get; set; }

        [JsonPropertyName("accountOwnerName")]
        public string? AccountOwnerName { get; set; }

        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("phoneNumber")]
        public string PhoneNumber { get; set; } = "";

        [JsonPropertyName("countryCode")]
        public string? CountryCode { get; set; }

        [JsonPropertyName("userEmail")]
        public string? UserEmail { get; set; }

        [JsonPropertyName("dob")]
        public DateTime? Dob { get; set; }

        [JsonPropertyName("hasPartner")]
        public bool? HasPartner { get; set; }

        [JsonPropertyName("permissionMessage")]
        public string? PermissionMessage { get; set; }

        [JsonPropertyName("providerIds")]
        public List<int> ProviderIds { get; set; } = new();

        [JsonPropertyName("providers")]
        public List<ProviderInfo> Providers { get; set; } = new();

        [JsonPropertyName("partner")]
        public object? Partner { get; set; }

        [JsonPropertyName("caregivers")]
        public List<PartnerDto> Caregivers { get; set; } = new();

        [JsonPropertyName("children")]
        public List<FamilyMemberDto> Children { get; set; } = new();

        // Helper for UI compatibility if needed
        public string Name => $"{(FirstName ?? "").Trim()} {(LastName ?? "").Trim()}".Trim();
    }

    public class FamilyMemberDto
    {
        [JsonPropertyName("contactId")]
        public int ContactId { get; set; }

        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("dob")]
        public DateTime? Dob { get; set; }

        [JsonPropertyName("contactGeneder")]
        public string? ContactGeneder { get; set; }

        [JsonPropertyName("contactGender")]
        public string? ContactGender
        {
            get => ContactGeneder;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    ContactGeneder = value;
                }
            }
        }

        [JsonPropertyName("contactAddedDatetime")]
        public DateTime? ContactAddedDatetime { get; set; }

        [JsonPropertyName("researchId")]
        public string? ResearchId { get; set; }

        [JsonPropertyName("relationKind")]
        public int? RelationKind { get; set; }

        public string Name => $"{(FirstName ?? "").Trim()} {(LastName ?? "").Trim()}".Trim();
    }

    public class Child
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string? ContactGender { get; set; }
    }

    public class Parent
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Phone { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class FamilyGroup
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; } = "";
        public List<Parent> Parents { get; set; } = new();
        public List<Child> Children { get; set; } = new();
    }

    public class ProviderInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }
    }
}
