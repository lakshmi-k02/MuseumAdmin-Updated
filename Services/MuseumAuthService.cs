using System.Net.Http.Json;
using System.Text.Json;
using MuseumAdmin.Models;

namespace MuseumAdmin.Services
{
    public class MuseumAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MuseumAuthService> _logger;

        public MuseumAuthService(HttpClient httpClient, ILogger<MuseumAuthService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            try
            {
                // 1. Force Request to use camelCase (UserName -> userName)
                // This is critical for the API to recognize the fields
                var response = await _httpClient.PostAsJsonAsync(
                    "api/Musium/MusiumLogin", 
                    request,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                // 2. Read the RAW string content first
        var jsonContent = await response.Content.ReadAsStringAsync();
        
        // 3. DEBUG PRINT: Print to Console or Debug Output
        Console.WriteLine($"[API DEBUG] Status: {response.StatusCode}");
        Console.WriteLine($"[API DEBUG] JSON: {jsonContent}"); 
        // Or use _logger.LogInformation($"[API DEBUG] {jsonContent}");


                if (response.IsSuccessStatusCode)
                {
                    // 2. Force Response to be Case-Insensitive (status -> Status)
                    var options = new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    };
                    
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>(options);
                    
                    if (result != null && result.Status)
                    {
                        return result;
                    }
                    
                    // Log the API message if status is false
                    _logger.LogWarning($"API returned 200 but failed status. Message: {result?.Message}");
                }
                else 
                {
                    _logger.LogWarning($"API Error: {response.StatusCode}");
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Login Exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Changes an authenticated user's password via the ChangeMuseumUserPassword API.
        /// Adheres to HIPAA and security standards: passwords are never logged.
        /// </summary>
        /// <param name="request">Payload containing user ID, username, current password, and new password confirmation.</param>
        /// <returns>ChangePasswordResponse with status and server message.</returns>
        public async Task<ChangePasswordResponse> ChangePasswordAsync(ChangePasswordRequest request)
        {
            try
            {
                _logger.LogInformation("Attempting password update for user ID: {UserId}, userName: {UserName}", request.UserId, request.UserName);

                var response = await _httpClient.PostAsJsonAsync(
                    "api/Musium/ChangeMuseumUserPassword",
                    request,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                var rawContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("ChangeMuseumUserPassword HTTP Status: {StatusCode}", response.StatusCode);

                if (!string.IsNullOrWhiteSpace(rawContent))
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var result = JsonSerializer.Deserialize<ChangePasswordResponse>(rawContent, options);

                    if (result != null)
                    {
                        return result;
                    }
                }

                if (response.IsSuccessStatusCode)
                {
                    return new ChangePasswordResponse
                    {
                        Status = true,
                        StatusCode = (int)response.StatusCode,
                        Message = "Password updated successfully."
                    };
                }

                return new ChangePasswordResponse
                {
                    Status = false,
                    StatusCode = (int)response.StatusCode,
                    Message = $"Request failed with status code {response.StatusCode}."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered while changing password for user ID: {UserId}", request.UserId);
                return new ChangePasswordResponse
                {
                    Status = false,
                    StatusCode = 500,
                    Message = "An unexpected network or server error occurred. Please try again."
                };
            }
        }
    }
}
