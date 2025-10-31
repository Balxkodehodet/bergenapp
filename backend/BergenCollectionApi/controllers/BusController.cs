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
}

