using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using BergenCollectionApi.models;

namespace BergenCollectionApi.Controllers;

[ApiController]
[Route("api")]
public class BikeController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BikeController> _logger;
    private readonly BikeDataCache _cache;

    public BikeController(HttpClient httpClient, ILogger<BikeController> logger, BikeDataCache cache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
    }

    [HttpGet("bike-data")]
    public IActionResult GetBikeData([FromQuery] BikeStationQuery query)
    {
        try
        {
            var merged = _cache.Get();

            IEnumerable<Dictionary<string, object>> filtered = merged;

            if (query.MinBikes.HasValue)
                filtered = filtered.Where(s => s.ContainsKey("num_bikes_available") && Convert.ToInt32(s["num_bikes_available"]) >= query.MinBikes.Value);

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
