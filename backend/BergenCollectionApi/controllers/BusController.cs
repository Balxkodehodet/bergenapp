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
  private readonly StopsDbContext _dbContext;

  public BusController(HttpClient httpClient, ILogger<BusController> logger, StopsDbContext dbContext)
  {
    _httpClient = httpClient;
    _logger = logger;
    _dbContext = dbContext;
  }

  [HttpGet("buss-data")]
  public async Task<IActionResult> GetBusData([FromQuery] BusQuery busQuery)
  {
    // Null checks and fallback defaults
    if (string.IsNullOrWhiteSpace(busQuery.stopPlaceId) || busQuery.timeRange == null || busQuery.numberOfDepartures == null)
    {
      return BadRequest(new { error = "Please provide stopPlaceId, timeRange, and numberOfDepartures." });
    }

    try
    {
      var query = $@"
        {{
          stopPlace(id: ""{busQuery.stopPlaceId}"") {{
            name
            estimatedCalls(timeRange: {busQuery.timeRange}, numberOfDepartures: {busQuery.numberOfDepartures}) {{
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
      var data = JsonSerializer.Deserialize<object>(content);

      return Ok(data);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error fetching bus data");
      return StatusCode(500, new { error = "Failed to fetch" });
    }
  }

  [HttpGet("bus-departures-by-name")]
  public async Task<IActionResult> GetBusDeparturesByStopName(
      [FromQuery] string stopName,
      [FromQuery] int timeRange = 7200,
      [FromQuery] int numberOfDepartures = 15)
  {
    if (string.IsNullOrWhiteSpace(stopName))
    {
      return BadRequest(new { error = "Please provide a stop name." });
    }

    try
    {
      // First, find the stop by name
      var matchingStops = _dbContext.Stops
          .Where(s => s.StopName.Contains(stopName))
          .Select(s => new { s.StopId, s.StopName })
          .ToList();

      if (!matchingStops.Any())
      {
        return NotFound(new { error = $"No stops found matching '{stopName}'" });
      }

      // If multiple stops found, you might want to return them for user selection
      // For now, we'll use the first match
      var selectedStop = matchingStops.First();

      // Now get bus departure data using the stop ID
      var query = $@"
        {{
          stopPlace(id: ""{selectedStop.StopId}"") {{
            name
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
      var data = JsonSerializer.Deserialize<object>(content);

      return Ok(new
      {
        selectedStop = selectedStop,
        alternativeStops = matchingStops.Skip(1).Take(4), // Show up to 4 alternatives
        departureData = data
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error fetching bus departures by stop name");
      return StatusCode(500, new { error = "Failed to fetch bus departures" });
    }
  }
}

