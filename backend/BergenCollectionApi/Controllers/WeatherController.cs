using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BergenCollectionApi.Controllers;

[ApiController]
[Route("api")]
public class WeatherController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(HttpClient httpClient, ILogger<WeatherController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [HttpGet("bergen-temp")]
    public async Task<IActionResult> GetBergenTemperature()
    {
        try
        {
            var url = "https://api.met.no/weatherapi/locationforecast/2.0/compact?lat=60.3913&lon=5.3221";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "balx042025@gmail.com");

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<object>(content);

            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weather data");
            return StatusCode(500, new { error = "Failed to fetch" });
        }
    }
}