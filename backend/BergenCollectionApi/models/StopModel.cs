using System.Globalization;

namespace BergenCollectionApi.models;

public class Stop
{
    public string StopId { get; set; } = string.Empty;
    public string StopName { get; set; } = string.Empty;
    public double StopLat { get; set; }
    public double StopLon { get; set; }
    public string? StopDesc { get; set; }
    public string? LocationType { get; set; }
    public string? ParentStation { get; set; }
    public string? WheelchairBoarding { get; set; }
    public string? VehicleType { get; set; }
    public string? PlatformCode { get; set; }
}

public static class StopParser
{
    public static List<Stop> ParseStopsTxt(string filePath)
    {
        var stops = new List<Stop>();
        var lines = File.ReadAllLines(filePath);

        // Skip header
        foreach (var line in lines.Skip(1))
        {
            var fields = line.Split(',');

            if (fields.Length < 4) continue; // Basic validation

            stops.Add(new Stop
            {
                StopId = fields[0] ?? string.Empty,
                StopName = fields[1] ?? string.Empty,
                StopLat = double.TryParse(fields[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) ? lat : 0,
                StopLon = double.TryParse(fields[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var lon) ? lon : 0,
                StopDesc = fields.Length > 4 ? fields[4] : null,
                LocationType = fields.Length > 5 ? fields[5] : null,
                ParentStation = fields.Length > 6 ? fields[6] : null,
                WheelchairBoarding = fields.Length > 7 ? fields[7] : null,
                VehicleType = fields.Length > 8 ? fields[8] : null,
                PlatformCode = fields.Length > 9 ? fields[9] : null
            });
        }

        return stops;
    }
}