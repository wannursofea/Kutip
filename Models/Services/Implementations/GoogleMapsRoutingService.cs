using Kutip.Models;
using Kutip.Services.Interfaces;
using System.Text.Json;

namespace Kutip.Services.Implementations
{
    public class GoogleMapsRoutingService : IRoutingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GoogleMapsRoutingService> _logger;

        public GoogleMapsRoutingService(HttpClient httpClient, string apiKey, ILogger<GoogleMapsRoutingService> logger)
        {
            _httpClient = httpClient;
            _apiKey = apiKey;
            _logger = logger;
        }

        public async Task<double?> GetDrivingDistance(Coordinates origin, Coordinates destination)
        {
            if (origin == null || destination == null)
            {
                _logger.LogWarning("Origin or destination coordinates are null for distance calculation.");
                return null;
            }

            string url = $"https://maps.googleapis.com/maps/api/distancematrix/json?origins={origin.Latitude},{origin.Longitude}&destinations={destination.Latitude},{destination.Longitude}&mode=driving&key={_apiKey}";

            try
            {
                _logger.LogInformation($"Calculating distance from {origin.Latitude},{origin.Longitude} to {destination.Latitude},{destination.Longitude}");
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();

                using (JsonDocument doc = JsonDocument.Parse(jsonString))
                {
                    JsonElement root = doc.RootElement;
                    string status = root.GetProperty("status").GetString();

                    if (status == "OK")
                    {
                        if (root.TryGetProperty("rows", out JsonElement rows) && rows.EnumerateArray().Any())
                        {
                            if (rows[0].TryGetProperty("elements", out JsonElement elements) && elements.EnumerateArray().Any())
                            {
                                JsonElement element = elements[0];
                                string elementStatus = element.GetProperty("status").GetString();

                                if (elementStatus == "OK")
                                {
                                    JsonElement distance = element.GetProperty("distance");
                                    return distance.GetProperty("value").GetDouble(); // Distance in meters
                                }
                                else
                                {
                                    _logger.LogWarning($"Distance Matrix API element status not OK: '{elementStatus}' for {origin.Latitude},{origin.Longitude} to {destination.Latitude},{destination.Longitude}");
                                }
                            }
                        }
                    }
                    else
                    {
                        string errorMessage = root.TryGetProperty("error_message", out JsonElement errorMsg) ? errorMsg.GetString() : "Unknown error";
                        _logger.LogError($"Google Distance Matrix API returned status '{status}'. Error: {errorMessage}");
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, $"HTTP request error during distance calculation. Message: {httpEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, $"JSON parsing error during distance calculation. Message: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred during distance calculation. Message: {ex.Message}");
            }

            return null;
        }

        public async Task<DistanceMatrixResult?> GetDistanceMatrix(List<Coordinates> origins, List<Coordinates> destinations)
        {
            if (!origins.Any() || !destinations.Any())
            {
                _logger.LogWarning("Origins or destinations list is empty for distance matrix calculation.");
                return null;
            }

            var originString = string.Join("|", origins.Select(c => $"{c.Latitude},{c.Longitude}"));
            var destinationString = string.Join("|", destinations.Select(c => $"{c.Latitude},{c.Longitude}"));

            string url = $"https://maps.googleapis.com/maps/api/distancematrix/json?origins={originString}&destinations={destinationString}&mode=driving&key={_apiKey}";

            try
            {
                _logger.LogInformation($"Calculating distance matrix for {origins.Count} origins and {destinations.Count} destinations.");
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();

                // Use JsonSerializer to deserialize the whole response into DistanceMatrixResult
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<DistanceMatrixResult>(jsonString, options);

                if (result?.Status == "OK")
                {
                    return result;
                }
                else
                {
                    _logger.LogError($"Google Distance Matrix API returned status '{result?.Status}'. Raw JSON: {jsonString}");
                    return null;
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, $"HTTP request error during distance matrix calculation. Message: {httpEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, $"JSON parsing error during distance matrix calculation. Message: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred during distance matrix calculation. Message: {ex.Message}");
            }

            return null;
        }
    }
}