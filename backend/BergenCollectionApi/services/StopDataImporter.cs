using System.IO.Compression;
using System.Globalization;


public class StopDataImporter : BackgroundService
{
    private readonly ILogger<StopDataImporter> _logger;
    private readonly string _gtfsZipUrl = "https://storage.googleapis.com/marduk-production/outbound/gtfs/rb_sky-aggregated-gtfs.zip";
    private readonly string _zipPath = "database/zipCache/gtfs.zip";
    private readonly string _extractPath = "database/zipCache/stops.txt";
    private readonly StopsDbContext _dbContext;


    public StopDataImporter(ILogger<StopDataImporter> logger, StopsDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DownloadGtfsZipAsync();
                ExtractStopsTxt();
                var stops = ParseStopsTxt(_extractPath);

                _dbContext.Stops.RemoveRange(_dbContext.Stops);
                await _dbContext.SaveChangesAsync();

                _dbContext.Stops.AddRange(stops);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("GTFS stops.txt updated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update GTFS stops.txt");
            }

            // Calculate delay until next 14:00 CEST
            var now = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"));
            var nextRun = now.Date.AddHours(14); // today at 14:00
            if (now >= nextRun)
                nextRun = nextRun.AddDays(1); // tomorrow at 14:00

            var delay = nextRun - now;
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task DownloadGtfsZipAsync()
    {
        using var client = new HttpClient();
        var zipBytes = await client.GetByteArrayAsync(_gtfsZipUrl);
        Directory.CreateDirectory(Path.GetDirectoryName(_zipPath)!);
        await File.WriteAllBytesAsync(_zipPath, zipBytes);
    }

    private void ExtractStopsTxt()
    {
        using var archive = ZipFile.OpenRead(_zipPath);
        var entry = archive.GetEntry("stops.txt");
        if (entry != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_extractPath)!);
            entry.ExtractToFile(_extractPath, overwrite: true);
        }
        else
        {
            _logger.LogWarning("stops.txt not found in GTFS zip archive.");
        }
    }

    private List<Stop> ParseStopsTxt(string filePath)
    {
        var stops = new List<Stop>();
        if (!File.Exists(filePath))
        {
            _logger.LogWarning($"File not found: {filePath}");
            return stops;
        }

        var lines = File.ReadAllLines(filePath);

        foreach (var line in lines.Skip(1)) // Skip header
        {
            var fields = line.Split(',');

            if (fields.Length < 10) continue; // Ensure all columns are present

            stops.Add(new Stop
            {
                StopId = fields[0],
                StopName = fields[1],
                StopLat = double.TryParse(fields[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) ? lat : 0,
                StopLon = double.TryParse(fields[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var lon) ? lon : 0,
                StopDesc = fields[4],
                LocationType = fields[5],
                ParentStation = fields[6],
                WheelchairBoarding = fields[7],
                VehicleType = fields[8],
                PlatformCode = fields[9]
            });
        }

        return stops;
    }
}