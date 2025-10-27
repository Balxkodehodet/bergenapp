using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BergenCollectionApi.Controllers;

[ApiController]
[Route("api")]
public class BikeController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BikeController> _logger;

    public BikeController(HttpClient httpClient, ILogger<BikeController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [HttpGet("bike-data")]
    public async Task<IActionResult> GetBikeData([FromQuery] BikeStationQuery query)
    {
        try
        {
            var stationStatUrl = "https://gbfs.urbansharing.com/bergenbysykkel.no/station_status.json";
            var stationInfoUrl = "https://gbfs.urbansharing.com/bergenbysykkel.no/station_information.json";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BergenApp/1.0 (balx042025@gmail.com)");

            var statusTask = _httpClient.GetStringAsync(stationStatUrl);
            var infoTask = _httpClient.GetStringAsync(stationInfoUrl);

            await Task.WhenAll(statusTask, infoTask);

            var statusData = JsonSerializer.Deserialize<JsonElement>(statusTask.Result);
            var infoData = JsonSerializer.Deserialize<JsonElement>(infoTask.Result);

            var statusStations = statusData.GetProperty("data").GetProperty("stations").EnumerateArray();
            var infoStations = infoData.GetProperty("data").GetProperty("stations").EnumerateArray().ToList();

            var merged = statusStations.Select(station =>
            {
                var stationId = station.GetProperty("station_id").GetString();
                var matchingInfo = infoStations.FirstOrDefault(info =>
                    info.GetProperty("station_id").GetString() == stationId);

                var result = new Dictionary<string, object>();

                // Add info properties first
                if (matchingInfo.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in matchingInfo.EnumerateObject())
                    {
                        result[prop.Name] = GetJsonValue(prop.Value);
                    }
                }

                // Add status properties (these will override info properties if they have the same name)
                foreach (var prop in station.EnumerateObject())
                {
                    result[prop.Name] = GetJsonValue(prop.Value);
                }

                return result;
            }).ToList();

            IEnumerable<Dictionary<string, object>> filtered = merged;

            if (query.MinBikes.HasValue)
                filtered = filtered.Where(s => s.ContainsKey("num_bikes_available") && Convert.ToInt32(s["num_bikes_available"]) >= query.MinBikes.Value);

            // Example location filter (implement GetDistanceKm as needed)
            if (query.Lat.HasValue && query.Lon.HasValue && query.RadiusKm.HasValue)
                filtered = filtered.Where(s =>
                    s.ContainsKey("lat") && s.ContainsKey("lon") &&
                    GetDistanceKm(Convert.ToDouble(s["lat"]), Convert.ToDouble(s["lon"]), query.Lat.Value, query.Lon.Value) <= query.RadiusKm.Value);

            return Ok(filtered.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bike data");
            return StatusCode(500, new { error = "Failed to fetch" });
        }
    }

    private static object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private static double GetDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth radius in kilometers
        var latRad1 = Math.PI * lat1 / 180.0;
        var latRad2 = Math.PI * lat2 / 180.0;
        var deltaLat = Math.PI * (lat2 - lat1) / 180.0;
        var deltaLon = Math.PI * (lon2 - lon1) / 180.0;

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(latRad1) * Math.Cos(latRad2) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
