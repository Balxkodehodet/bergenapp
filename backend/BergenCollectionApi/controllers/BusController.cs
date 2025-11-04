using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace BergenCollectionApi.Controllers;

[ApiController]
[Route("api")]
public class BusController : ControllerBase
{
  private readonly HttpClient _httpClient;
  private readonly ILogger<BusController> _logger;

  public BusController(HttpClient httpClient, ILogger<BusController> logger)
  {
    _httpClient = httpClient;
    _logger = logger;
  }

  [HttpGet("bus-departures")]
  public async Task<IActionResult> GetBusDepartures(
      [FromQuery] string stopId,
      [FromQuery] int timeRange = 7200,
      [FromQuery] int numberOfDepartures = 15)
  {
    if (string.IsNullOrWhiteSpace(stopId))
    {
      return BadRequest(new { error = "Please provide a stopId." });
    }

    try
    {
      var query = $@"
            {{
              stopPlace(id: ""{stopId}"") {{
                name
                id
                estimatedCalls(timeRange: {timeRange}, numberOfDepartures: {numberOfDepartures}) {{
                  realtime
                  aimedDepartureTime
                  expectedDepartureTime
                  destinationDisplay {{
                    frontText
                  }}
                  serviceJourney {{
                    line {{
                      id
                      name
                      transportMode
                    }}
                  }}
                }}
              }}
            }}";

      var requestBody = new StringContent(JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json");

      var request = new HttpRequestMessage(HttpMethod.Post, "https://api.entur.io/journey-planner/v3/graphql")
      {
        Content = requestBody
      };
      request.Headers.Add("ET-Client-Name", "student/Bergen-app");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();

      if (string.IsNullOrWhiteSpace(content))
      {
        return StatusCode(500, new { error = "Empty response from Entur API" });
      }

      var data = JsonSerializer.Deserialize<JsonElement>(content);

      // Validate response structure
      if (!data.TryGetProperty("data", out var dataElement) ||
          !dataElement.TryGetProperty("stopPlace", out var stopPlaceElement) ||
          stopPlaceElement.ValueKind == JsonValueKind.Null)
      {
        return NotFound(new { error = $"Stop {stopId} not found or inactive" });
      }

      return Ok(new
      {
        stopId = stopId,
        data = stopPlaceElement,
        requestedAt = DateTime.UtcNow
      });
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "HTTP error when fetching departures for stop {StopId}", stopId);
      return StatusCode(503, new { error = "Entur API unavailable" });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error fetching departures for stop {StopId}", stopId);
      return StatusCode(500, new { error = "Failed to fetch departures" });
    }
  }

  [HttpGet("bus-departures-multiple")]
  public async Task<IActionResult> GetMultipleBusDepartures(
      [FromQuery] string[] stopIds,
      [FromQuery] int timeRange = 7200,
      [FromQuery] int numberOfDepartures = 15)
  {
    if (stopIds == null || !stopIds.Any())
    {
      return BadRequest(new { error = "Please provide at least one stopId." });
    }

    var results = new List<object>();

    foreach (var stopId in stopIds.Take(10)) // Limit to 10 stops to prevent abuse
    {
      try
      {
        var query = $@"
                {{
                  stopPlace(id: ""{stopId}"") {{
                    name
                    id
                    estimatedCalls(timeRange: {timeRange}, numberOfDepartures: {numberOfDepartures}) {{
                      realtime
                      aimedDepartureTime
                      expectedDepartureTime
                      destinationDisplay {{
                        frontText
                      }}
                      serviceJourney {{
                        line {{
                          id
                          name
                          transportMode
                        }}
                      }}
                    }}
                  }}
                }}";

        var requestBody = new StringContent(JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.entur.io/journey-planner/v3/graphql")
        {
          Content = requestBody
        };
        request.Headers.Add("ET-Client-Name", "student/Bergen-app");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(content);

        if (data.TryGetProperty("data", out var dataElement) &&
            dataElement.TryGetProperty("stopPlace", out var stopPlaceElement) &&
            stopPlaceElement.ValueKind != JsonValueKind.Null)
        {
          results.Add(new
          {
            stopId = stopId,
            success = true,
            data = stopPlaceElement
          });
        }
        else
        {
          results.Add(new
          {
            stopId = stopId,
            success = false,
            error = "Stop not found or inactive"
          });
        }
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Error fetching data for stop {StopId}", stopId);
        results.Add(new
        {
          stopId = stopId,
          success = false,
          error = ex.Message
        });
      }
    }

    return Ok(new
    {
      requestedStops = stopIds.Length,
      processedStops = results.Count,
      results = results,
      requestedAt = DateTime.UtcNow
    });
  }
}

