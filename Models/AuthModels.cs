using System.Text.Json.Serialization;

namespace MuseumAdmin.Models
{
    public class LoginRequest
    {
        public string UserText { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Status { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public UserData? User { get; set; }

        [JsonPropertyName("data2")]
        public MuseumData? Museum { get; set; }
    }

    public class UserData
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        
        [JsonPropertyName("musesumId")] // Maps the API typo to a clean property
        public int MuseumId { get; set; }
        
        public string DisplayApplicationURL { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        // Add other fields as needed
    }

    public class MuseumData
    {
        public int Id { get; set; }
        public string MuseumName { get; set; } = string.Empty;
        public string MuseumCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request payload for changing an authenticated user's password.
    /// </summary>
    public class ChangePasswordRequest
    {
        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        [JsonPropertyName("currentPassword")]
        public string CurrentPassword { get; set; } = string.Empty;

        [JsonPropertyName("newPassword")]
        public string NewPassword { get; set; } = string.Empty;

        [JsonPropertyName("confirmPassword")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model returned by the ChangeMuseumUserPassword API endpoint.
    /// </summary>
    public class ChangePasswordResponse
    {
        [JsonPropertyName("status")]
        public bool Status { get; set; }

        [JsonPropertyName("statusCode")]
        public int? StatusCode { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }
}
