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
      // Find all matching stops
      var matchingStops = _dbContext.Stops
          .Where(s => s.StopName.Contains(stopName))
          .Select(s => new { s.StopId, s.StopName })
          .ToList();

      if (!matchingStops.Any())
      {
        return NotFound(new { error = $"No stops found matching '{stopName}'" });
      }

      var allDepartures = new List<object>();
      var successfulStops = new List<object>();

      // Query each stop ID and collect all departure data
      foreach (var stop in matchingStops)
      {
        try
        {
          var query = $@"
                {{
                  stopPlace(id: ""{stop.StopId}"") {{
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

          // Add null/empty content check
          if (string.IsNullOrWhiteSpace(content))
          {
            _logger.LogWarning("Empty response received for stop {StopId}", stop.StopId);
            continue;
          }

          var data = JsonSerializer.Deserialize<JsonElement>(content);

          // Enhanced null checking with more specific logging
          if (!data.TryGetProperty("data", out var dataElement))
          {
            _logger.LogWarning("No 'data' property found in response for stop {StopId}", stop.StopId);
            continue;
          }

          if (!dataElement.TryGetProperty("stopPlace", out var stopPlaceElement))
          {
            _logger.LogWarning("No 'stopPlace' property found for stop {StopId}", stop.StopId);
            continue;
          }

          if (stopPlaceElement.ValueKind == JsonValueKind.Null)
          {
            _logger.LogWarning("StopPlace is null for stop {StopId} - stop may not exist or be inactive", stop.StopId);
            continue;
          }

          // Check if estimatedCalls exists and has data
          if (!stopPlaceElement.TryGetProperty("estimatedCalls", out var estimatedCalls))
          {
            _logger.LogInformation("No 'estimatedCalls' property found for stop {StopId} - no departure data available", stop.StopId);

            // Still add to successful stops but without departure data
            successfulStops.Add(new
            {
              stopId = stop.StopId,
              stopName = stop.StopName,
              departureData = stopPlaceElement,
              hasDepartures = false
            });
            continue;
          }

          if (estimatedCalls.ValueKind == JsonValueKind.Null)
          {
            _logger.LogInformation("EstimatedCalls is null for stop {StopId} - no departures scheduled", stop.StopId);

            successfulStops.Add(new
            {
              stopId = stop.StopId,
              stopName = stop.StopName,
              departureData = stopPlaceElement,
              hasDepartures = false
            });
            continue;
          }

          // Check if estimatedCalls array is empty
          var callsArray = estimatedCalls.EnumerateArray().ToList();
          if (!callsArray.Any())
          {
            _logger.LogInformation("No departures found for stop {StopId} in the specified time range", stop.StopId);

            successfulStops.Add(new
            {
              stopId = stop.StopId,
              stopName = stop.StopName,
              departureData = stopPlaceElement,
              hasDepartures = false
            });
            continue;
          }

          // Successfully found departures
          successfulStops.Add(new
          {
            stopId = stop.StopId,
            stopName = stop.StopName,
            departureData = stopPlaceElement,
            hasDepartures = true
          });

          // Process each departure with additional null checks
          foreach (var call in callsArray)
          {
            try
            {
              // Validate essential departure data exists
              if (!call.TryGetProperty("expectedDepartureTime", out var expectedTime) ||
                  expectedTime.ValueKind == JsonValueKind.Null)
              {
                _logger.LogWarning("Missing expectedDepartureTime for a departure at stop {StopId}", stop.StopId);
                continue;
              }

              if (!call.TryGetProperty("destinationDisplay", out var destDisplay) ||
                  destDisplay.ValueKind == JsonValueKind.Null ||
                  !destDisplay.TryGetProperty("frontText", out var frontText) ||
                  frontText.ValueKind == JsonValueKind.Null)
              {
                _logger.LogWarning("Missing destination information for a departure at stop {StopId}", stop.StopId);
                continue;
              }

              // Add stop context to each departure
              var callWithContext = new Dictionary<string, object>
              {
                ["stopId"] = stop.StopId,
                ["stopName"] = stop.StopName,
                ["departure"] = JsonSerializer.Deserialize<JsonElement>(call.GetRawText()) // Changed from object to JsonElement
              };
              allDepartures.Add(callWithContext);
            }
            catch (Exception callEx)
            {
              _logger.LogWarning(callEx, "Error processing individual departure at stop {StopId}", stop.StopId);
              // Continue processing other departures
            }
          }
        }
        catch (HttpRequestException httpEx)
        {
          _logger.LogWarning(httpEx, "HTTP error when fetching data for stop {StopId}", stop.StopId);
          continue;
        }
        catch (JsonException jsonEx)
        {
          _logger.LogWarning(jsonEx, "JSON parsing error for stop {StopId}", stop.StopId);
          continue;
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "General error when processing stop {StopId}", stop.StopId);
          continue;
        }
      }

      // Update the sorting logic with additional null checks
      var sortedDepartures = allDepartures
          .Where(d =>
          {
            if (d is not Dictionary<string, object> dict) return false;
            if (!dict.ContainsKey("departure")) return false;
            if (dict["departure"] is not JsonElement departure) return false;
            return departure.TryGetProperty("expectedDepartureTime", out var timeElement) &&
                   timeElement.ValueKind != JsonValueKind.Null &&
                   !string.IsNullOrWhiteSpace(timeElement.GetString());
          })
          .OrderBy(d =>
          {
            try
            {
              var dict = (Dictionary<string, object>)d;
              var departure = (JsonElement)dict["departure"];
              departure.TryGetProperty("expectedDepartureTime", out var timeElement);
              return DateTime.Parse(timeElement.GetString() ?? string.Empty);
            }
            catch
            {
              return DateTime.MaxValue; // Put invalid dates at the end
            }
          })
          .Take(numberOfDepartures)
          .ToList();

      // Enhanced response with more information
      return Ok(new
      {
        searchTerm = stopName,
        foundStops = matchingStops.Count,
        stopsWithData = successfulStops.Count,
        stopsWithDepartures = successfulStops.Count(s => s.GetType().GetProperty("hasDepartures")?.GetValue(s) as bool? == true),
        stops = successfulStops,
        departures = sortedDepartures,
        totalDepartures = sortedDepartures.Count,
        hasResults = sortedDepartures.Any()
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error fetching bus departures by stop name");
      return StatusCode(500, new { error = "Failed to fetch bus departures" });
    }
  }

  [HttpGet("bus-departures-by-name-smart")]
  public async Task<IActionResult> GetBusDeparturesByStopNameSmart(
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
      // Find all matching stops
      var matchingStops = _dbContext.Stops
          .Where(s => s.StopName.Contains(stopName))
          .ToList();

      if (!matchingStops.Any())
      {
        return NotFound(new { error = $"No stops found matching '{stopName}'" });
      }

      // Prioritize stops based on certain criteria
      var prioritizedStops = matchingStops
          .OrderByDescending(s => s.StopName.Equals(stopName, StringComparison.OrdinalIgnoreCase)) // Exact matches first
          .ThenByDescending(s => string.IsNullOrEmpty(s.ParentStation)) // Prefer main stops over sub-stops
          .ThenByDescending(s => s.LocationType == "1") // Prefer station types
          .Take(3) // Limit to top 3 candidates
          .ToList();

      var allDepartures = new List<object>();
      var successfulStops = new List<object>();

      // Query each prioritized stop ID and collect departure data
      foreach (var stop in prioritizedStops)
      {
        try
        {
          var query = $@"
                {{
                  stopPlace(id: ""{stop.StopId}"") {{
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

          // Check if we got valid data
          if (data.TryGetProperty("data", out var dataElement) &&
              dataElement.TryGetProperty("stopPlace", out var stopPlaceElement) &&
              !stopPlaceElement.ValueKind.Equals(JsonValueKind.Null))
          {
            successfulStops.Add(new
            {
              stopId = stop.StopId,
              stopName = stop.StopName,
              departureData = stopPlaceElement
            });

            // Extract estimated calls if they exist
            if (stopPlaceElement.TryGetProperty("estimatedCalls", out var estimatedCalls))
            {
              foreach (var call in estimatedCalls.EnumerateArray())
              {
                // Add stop context to each departure
                var callWithContext = new Dictionary<string, object>
                {
                  ["stopId"] = stop.StopId,
                  ["stopName"] = stop.StopName,
                  ["departure"] = JsonSerializer.Deserialize<object>(call.GetRawText())
                };
                allDepartures.Add(callWithContext);
              }
            }
          }
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Failed to get data for stop {StopId}", stop.StopId);
          // Continue with other stops
        }
      }

      // Sort all departures by expected departure time
      var sortedDepartures = allDepartures
          .Where(d => d is Dictionary<string, object> dict &&
                     dict.ContainsKey("departure") &&
                     dict["departure"] is JsonElement departure &&
                     departure.TryGetProperty("expectedDepartureTime", out _))
          .OrderBy(d =>
          {
            var dict = (Dictionary<string, object>)d;
            var departure = (JsonElement)dict["departure"];
            departure.TryGetProperty("expectedDepartureTime", out var timeElement);
            return timeElement.GetString();
          })
          .Take(numberOfDepartures)
          .ToList();

      return Ok(new
      {
        searchTerm = stopName,
        foundStops = matchingStops.Count,
        stopsWithData = successfulStops.Count,
        stops = successfulStops,
        departures = sortedDepartures,
        totalDepartures = sortedDepartures.Count
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error fetching bus departures by stop name");
      return StatusCode(500, new { error = "Failed to fetch bus departures" });
    }
  }

  [HttpGet("stop-platforms")]
  public async Task<IActionResult> GetStopPlatforms([FromQuery] string stopName)
  {
    if (string.IsNullOrWhiteSpace(stopName))
    {
      return BadRequest(new { error = "Please provide a stop name." });
    }

    try
    {
      // First find stops that match the name
      var matchingStops = _dbContext.Stops
          .Where(s => s.StopName.Contains(stopName))
          .ToList();

      if (!matchingStops.Any())
      {
        return NotFound(new { error = $"No stops found matching '{stopName}'" });
      }

      // Group stops by their main station or treat each as individual if no parent
      var platformGroups = matchingStops
          .GroupBy(s => string.IsNullOrEmpty(s.ParentStation) ? s.StopId : s.ParentStation)
          .Select(group => new
          {
            mainStopId = group.Key,
            mainStopName = group.First(s => string.IsNullOrEmpty(s.ParentStation) || s.StopId == group.Key).StopName,
            hasMultiplePlatforms = group.Count() > 1,
            platformCount = group.Count(),
            platforms = group.Select(s => new
            {
              s.StopId,
              s.StopName,
              s.PlatformCode,
              s.ParentStation,
              isMainStop = string.IsNullOrEmpty(s.ParentStation)
            }).OrderBy(s => s.PlatformCode).ToList()
          })
          .ToList();

      return Ok(new
      {
        searchTerm = stopName,
        totalStopsFound = matchingStops.Count,
        stopGroups = platformGroups,
        stopsWithPlatforms = platformGroups.Count(g => g.hasMultiplePlatforms)
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error fetching platforms for stop name '{StopName}'", stopName);
      return StatusCode(500, new { error = "Failed to fetch platforms" });
    }
  }
}

