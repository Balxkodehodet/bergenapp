using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api")]
public class StopController : ControllerBase
{
    private readonly StopsDbContext _context;

    public StopController(StopsDbContext context)
    {
        _context = context;
    }

    [HttpGet("stops/search")]
    public IActionResult SearchStops([FromQuery] string query)
    {
        var stops = _context.Stops
            .Where(s => s.StopName.Contains(query))
            .Select(s => new { s.StopId, s.StopName })
            .ToList();
        return Ok(stops);
    }

    [HttpGet("stops/all")]
    public IActionResult GetAllStops()
    {
        var stops = _context.Stops
            .Select(s => new { s.StopId, s.StopName })
            .OrderBy(s => s.StopName)
            .ToList();
        return Ok(stops);
    }
}