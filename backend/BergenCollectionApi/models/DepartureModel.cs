using System.Text.Json;

public class DepartureWithContext
{
    public string StopId { get; set; } = string.Empty;
    public string StopName { get; set; } = string.Empty;
    public JsonElement Departure { get; set; }
}