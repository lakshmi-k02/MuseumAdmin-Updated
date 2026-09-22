using System.Net.Http.Json;
using System.Text.Json;
using MuseumAdmin.Models;
using MuseumAdmin.Models.Exhibits;

namespace MuseumAdmin.Services
{
    public class ExhibitService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ExhibitService> _logger;

        public ExhibitService(
            HttpClient httpClient,
            ILogger<ExhibitService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<ExhibitDto>> GetExhibitsAsync(int museumId)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"api/Musium/GetTravellingExhibits?museumId={museumId}");

                response.EnsureSuccessStatusCode();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var apiResponse =
                    await response.Content.ReadFromJsonAsync<ApiResponse<List<ExhibitDto>>>(options);

                return apiResponse?.Data ?? new List<ExhibitDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch exhibits");
                return new List<ExhibitDto>();
            }
        }
    }
}
