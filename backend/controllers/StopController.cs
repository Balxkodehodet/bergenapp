using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api")]
public class StopController : ControllerBase
{
    private readonly StopsDbContext _context;
    private readonly ILogger<StopController> _logger;

    public StopController(StopsDbContext context, ILogger<StopController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("stops/search")]
    public IActionResult SearchStops([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Please provide a search query." });
        }

        try
        {
            var stops = _context.Stops
                .Where(s => s.StopName.Contains(query))
                .Select(s => new
                {
                    s.StopId,
                    s.StopName,
                    s.PlatformCode,
                    s.ParentStation
                })
                .OrderBy(s => s.StopName)
                .ToList();

            return Ok(new
            {
                searchTerm = query,
                totalResults = stops.Count,
                stops = stops
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching stops for query: {Query}", query);
            return StatusCode(500, new { error = "Failed to search stops" });
        }
    }

    [HttpGet("stops/all")]
    public IActionResult GetAllStops()
    {
        try
        {
            var stops = _context.Stops
                .Select(s => new
                {
                    s.StopId,
                    s.StopName,
                    s.ParentStation
                })
                .OrderBy(s => s.StopName)
                .ToList(); // REMOVED .Take(1000) limit

            return Ok(new
            {
                totalStops = stops.Count,
                stops = stops
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all stops");
            return StatusCode(500, new { error = "Failed to fetch stops" });
        }
    }

    [HttpGet("stops/platforms")]
    public IActionResult GetStopPlatforms([FromQuery] string stopName)
    {
        if (string.IsNullOrWhiteSpace(stopName))
        {
            return BadRequest(new { error = "Please provide a stop name." });
        }

        try
        {
            // Find all stops matching the name
            var matchingStops = _context.Stops
                .Where(s => s.StopName.Contains(stopName))
                .ToList();

            if (!matchingStops.Any())
            {
                return NotFound(new { error = $"No stops found matching '{stopName}'" });
            }

            // Group by main stop (parent station or self if no parent)
            var platformGroups = matchingStops
                .GroupBy(s => string.IsNullOrEmpty(s.ParentStation) ? s.StopId : s.ParentStation)
                .Select(group => new
                {
                    mainStopId = group.Key,
                    mainStopName = group.First().StopName,
                    hasMultiplePlatforms = group.Count() > 1,
                    platforms = group.Select(s => new
                    {
                        s.StopId,
                        s.StopName,
                        s.PlatformCode,
                        isMainStop = string.IsNullOrEmpty(s.ParentStation)
                    }).OrderBy(s => s.PlatformCode).ToList()
                })
                .ToList();

            return Ok(new
            {
                searchTerm = stopName,
                totalStops = matchingStops.Count,
                stopGroups = platformGroups
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching platforms for stop: {StopName}", stopName);
            return StatusCode(500, new { error = "Failed to fetch platforms" });
        }
    }
}