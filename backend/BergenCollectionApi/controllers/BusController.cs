using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using BergenCollectionApi.data;
using Microsoft.EntityFrameworkCore;

namespace BergenCollectionApi.Controllers;

[ApiController]
[Route("api")]
public class BusController : ControllerBase
{
  private readonly HttpClient _httpClient;
  private readonly ILogger<BusController> _logger;
  private readonly StopsDbContext _dbContext; // Add this

  public BusController(HttpClient httpClient, ILogger<BusController> logger, StopsDbContext dbContext) // Add dbContext parameter
  {
    _httpClient = httpClient;
    _logger = logger;
    _dbContext = dbContext; // Add this
  }

  /// <summary>
  /// Resolve an identifier that might be a Quay (platform) to its owning StopPlace (station),
  /// because Entur's GraphQL field stopPlace expects a StopPlace id, not a Quay id.
  /// If the id already points to a StopPlace or no mapping is found, returns the original id.
  /// </summary>
  private string ResolveStopPlaceId(string stopId)
  {
    if (string.IsNullOrWhiteSpace(stopId)) return stopId;

    // If already a StopPlace id, use as-is
    if (stopId.StartsWith("NSR:StopPlace:", StringComparison.OrdinalIgnoreCase))
    {
      return stopId;
    }

    try
    {
      // Try to map Quay -> ParentStation (StopPlace) using local DB
      var stop = _dbContext.Stops
        .AsNoTracking()
        .FirstOrDefault(s => s.StopId == stopId);

      if (stop != null && !string.IsNullOrWhiteSpace(stop.ParentStation))
      {
        return stop.ParentStation!;
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to resolve StopPlace id for {StopId}", stopId);
    }

    // Fallback: return original id
    return stopId;
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
      var resolvedStopId = ResolveStopPlaceId(stopId);
      var query =
        "{" +
        "  stopPlace(id: \"" + resolvedStopId + "\") {" +
        "    name" +
        "    id" +
        "    estimatedCalls(timeRange: " + timeRange + ", numberOfDepartures: " + numberOfDepartures + ") {" +
        "      realtime" +
        "      aimedDepartureTime" +
        "      expectedDepartureTime" +
        "      destinationDisplay {" +
        "        frontText" +
        "      }" +
        "      serviceJourney {" +
        "        line {" +
        "          id" +
        "          name" +
        "          transportMode" +
        "        }" +
        "      }" +
        "    }" +
        "  }" +
        "}";

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
        return NotFound(new { error = $"Stop {resolvedStopId} not found or inactive" });
      }

      return Ok(new
      {
        requestedStopId = stopId,
        resolvedStopId = resolvedStopId,
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
        var resolvedStopId = ResolveStopPlaceId(stopId);
        var query =
          "{" +
          "  stopPlace(id: \"" + resolvedStopId + "\") {" +
          "    name" +
          "    id" +
          "    estimatedCalls(timeRange: " + timeRange + ", numberOfDepartures: " + numberOfDepartures + ") {" +
          "      realtime" +
          "      aimedDepartureTime" +
          "      expectedDepartureTime" +
          "      destinationDisplay {" +
          "        frontText" +
          "      }" +
          "      serviceJourney {" +
          "        line {" +
          "          id" +
          "          name" +
          "          transportMode" +
          "        }" +
          "      }" +
          "    }" +
          "  }" +
          "}";

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
            requestedStopId = stopId,
            resolvedStopId = resolvedStopId,
            success = true,
            data = stopPlaceElement
          });
        }
        else
        {
          results.Add(new
          {
            requestedStopId = stopId,
            resolvedStopId = resolvedStopId,
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
          requestedStopId = stopId,
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
      // First, find the stop by name using your StopsDbContext
      var matchingStops = _dbContext.Stops
          .Where(s => s.StopName.Contains(stopName))
          .Select(s => new { s.StopId, s.StopName, s.ParentStation })
          .AsNoTracking()
          .ToList();

      if (!matchingStops.Any())
      {
        return NotFound(new { error = $"No stops found matching '{stopName}'" });
      }

      // Prefer a station-level row (no ParentStation). If not available, fall back to first and use its ParentStation.
      var stationLevel = matchingStops.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.ParentStation));
      var selectedStop = stationLevel ?? matchingStops.First();
      var resolvedStopId = !string.IsNullOrWhiteSpace(selectedStop.ParentStation)
        ? selectedStop.ParentStation!
        : selectedStop.StopId;
      resolvedStopId = ResolveStopPlaceId(resolvedStopId);

      // Now get bus departure data using the existing logic
      var query =
        "{" +
        "  stopPlace(id: \"" + resolvedStopId + "\") {" +
        "    name" +
        "    id" +
        "    estimatedCalls(timeRange: " + timeRange + ", numberOfDepartures: " + numberOfDepartures + ") {" +
        "      realtime" +
        "      aimedDepartureTime" +
        "      expectedDepartureTime" +
        "      destinationDisplay {" +
        "        frontText" +
        "      }" +
        "      serviceJourney {" +
        "        line {" +
        "          id" +
        "          name" +
        "          transportMode" +
        "        }" +
        "      }" +
        "    }" +
        "  }" +
        "}";

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

      // Validate response structure
      if (!data.TryGetProperty("data", out var dataElement) ||
          !dataElement.TryGetProperty("stopPlace", out var stopPlaceElement) ||
          stopPlaceElement.ValueKind == JsonValueKind.Null)
      {
        return NotFound(new { error = $"Stop {resolvedStopId} not found or inactive" });
      }

      return Ok(new
      {
        requestedStopName = stopName,
        selectedStop = new { selectedStop.StopId, selectedStop.StopName, selectedStop.ParentStation },
        resolvedStopId = resolvedStopId,
        data = stopPlaceElement,
        requestedAt = DateTime.UtcNow
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error fetching bus departures by stop name");
      return StatusCode(500, new { error = "Failed to fetch bus departures" });
    }
  }
}

