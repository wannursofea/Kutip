using Kutip.Models;
using Kutip.Services.Interfaces;
using System.Text.Json;
using System.Web;

namespace Kutip.Services.Implementations
{
    public class GoogleMapsGeocodingService : IGeocodingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GoogleMapsGeocodingService> _logger;

        public GoogleMapsGeocodingService(HttpClient httpClient, IConfiguration configuration, ILogger<GoogleMapsGeocodingService> logger)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GoogleMapsApiKey"];
            _logger = logger;
        }

        public async Task<Coordinates?> GetCoordinates(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                _logger.LogWarning("Geocoding request received with empty address.");
                return null;
            }

            var encodedAddress = HttpUtility.UrlEncode(address);
            string url = $"https://maps.googleapis.com/maps/api/geocode/json?address={encodedAddress}&key={_apiKey}";

            try
            {
                _logger.LogInformation($"Geocoding address: {address}");
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode(); 

                var jsonString = await response.Content.ReadAsStringAsync();

                using (JsonDocument doc = JsonDocument.Parse(jsonString))
                {
                    JsonElement root = doc.RootElement;
                    string status = root.GetProperty("status").GetString();

                    if (status == "OK")
                    {
                        if (root.TryGetProperty("results", out JsonElement results) && results.EnumerateArray().Any())
                        {
                            JsonElement location = results[0].GetProperty("geometry").GetProperty("location");
                            double lat = location.GetProperty("lat").GetDouble();
                            double lng = location.GetProperty("lng").GetDouble();
                            return new Coordinates { Latitude = lat, Longitude = lng };
                        }
                    }
                    else if (status == "ZERO_RESULTS")
                    {
                        _logger.LogWarning($"Geocoding returned ZERO_RESULTS for address: {address}");
                    }
                    else
                    {
                        string errorMessage = root.TryGetProperty("error_message", out JsonElement errorMsg) ? errorMsg.GetString() : "Unknown error";
                        _logger.LogError($"Google Geocoding API returned status '{status}' for address '{address}'. Error: {errorMessage}");
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, $"HTTP request error during geocoding for address: {address}. Message: {httpEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, $"JSON parsing error during geocoding for address: {address}. Message: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred during geocoding for address: {address}. Message: {ex.Message}");
            }

            return null; 
        }
    }
}