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
    public async Task<IActionResult> GetBikeData()
    {
        try
        {
            var stationStatUrl = "https://gbfs.urbansharing.com/bergenbysykkel.no/station_status.json";
            var stationInfoUrl = "https://gbfs.urbansharing.com/bergenbysykkel.no/station_information.json";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "balx042025@gmail.com");

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
                if (matchingInfo.ValueKind != JsonValueKind.Undefined)
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

            return Ok(merged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bike data");
            return StatusCode(500, new { error = "Failed to fetch" });
        }
    }

    private static object GetJsonValue(JsonElement element)
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
}