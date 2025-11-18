using System.IO.Compression;
using System.Globalization;
using BergenCollectionApi.data;
using BergenCollectionApi.models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;


public class StopDataImporter : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StopDataImporter> _logger;
    private readonly string _gtfsZipUrl = "https://storage.googleapis.com/marduk-production/outbound/gtfs/rb_sky-aggregated-gtfs.zip";
    private readonly string _zipPath = "database/gtfs.zip";
    private readonly string _extractPath = "database/zipCache/stops.txt";


    public StopDataImporter(IServiceProvider serviceProvider, ILogger<StopDataImporter> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StopsDbContext>();

        try
        {
            // Ensure database and tables exist
            await dbContext.Database.EnsureCreatedAsync();
            _logger.LogInformation("Database ensured to exist");

            // Check if data already exists
            var existingStopsCount = await dbContext.Stops.CountAsync();
            if (existingStopsCount > 0)
            {
                _logger.LogInformation("Stop data already exists in database ({Count} stops)", existingStopsCount);
                return;
            }

            _logger.LogInformation("No stop data found, starting download and import process...");

            // Create directories if they don't exist
            var zipDir = Path.GetDirectoryName(_zipPath);
            if (!string.IsNullOrEmpty(zipDir))
            {
                Directory.CreateDirectory(zipDir);
                _logger.LogInformation("Created directory: {Directory}", zipDir);
            }

            await DownloadGtfsZipAsync();

            // Verify the zip file was downloaded
            if (!File.Exists(_zipPath))
            {
                _logger.LogError("GTFS zip file was not downloaded to {Path}", _zipPath);
                return;
            }

            _logger.LogInformation("GTFS zip file exists at {Path}, size: {Size} bytes", _zipPath, new FileInfo(_zipPath).Length);

            ExtractStopsTxt();

            // Verify the stops.txt file was extracted
            if (!File.Exists(_extractPath))
            {
                _logger.LogError("stops.txt was not extracted to {Path}", _extractPath);
                return;
            }

            _logger.LogInformation("stops.txt extracted to {Path}, size: {Size} bytes", _extractPath, new FileInfo(_extractPath).Length);

            var stops = ParseStopsTxt(_extractPath);

            if (stops.Any())
            {
                _logger.LogInformation("Parsed {Count} stops from GTFS file", stops.Count);

                // Add new data
                await dbContext.Stops.AddRangeAsync(stops);
                await dbContext.SaveChangesAsync();

                _logger.LogInformation("Successfully imported {Count} stops to database", stops.Count);
            }
            else
            {
                _logger.LogWarning("No stops were parsed from the GTFS file");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update GTFS stops.txt");
        }
    }

    private async Task DownloadGtfsZipAsync()
    {
        _logger.LogInformation("Downloading GTFS zip from {Url}", _gtfsZipUrl);

        // Create HttpClient with extended timeout
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(10); // 10 minutes timeout
        client.DefaultRequestHeaders.Add("User-Agent", "Bergen-App/1.0");

        try
        {
            var response = await client.GetAsync(_gtfsZipUrl);
            response.EnsureSuccessStatusCode();

            var zipBytes = await response.Content.ReadAsByteArrayAsync();

            var zipDir = Path.GetDirectoryName(_zipPath);
            if (!string.IsNullOrEmpty(zipDir))
            {
                Directory.CreateDirectory(zipDir);
            }

            await File.WriteAllBytesAsync(_zipPath, zipBytes);

            _logger.LogInformation("Downloaded GTFS zip ({Size} bytes) to {Path}", zipBytes.Length, _zipPath);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError("Download timed out after 10 minutes");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while downloading GTFS zip");
            throw;
        }
    }

    private void ExtractStopsTxt()
    {
        _logger.LogInformation("Extracting stops.txt from {ZipPath}", _zipPath);

        try
        {
            using var archive = ZipFile.OpenRead(_zipPath);
            var entry = archive.GetEntry("stops.txt");

            if (entry != null)
            {
                var extractDir = Path.GetDirectoryName(_extractPath);
                if (!string.IsNullOrEmpty(extractDir))
                {
                    Directory.CreateDirectory(extractDir);
                }

                entry.ExtractToFile(_extractPath, overwrite: true);
                _logger.LogInformation("Extracted stops.txt to {ExtractPath}", _extractPath);
            }
            else
            {
                _logger.LogError("stops.txt not found in GTFS zip archive");

                // Log what files ARE in the archive
                _logger.LogInformation("Files in archive:");
                foreach (var archiveEntry in archive.Entries)
                {
                    _logger.LogInformation("  - {FileName}", archiveEntry.FullName);
                }

                throw new FileNotFoundException("stops.txt not found in GTFS zip archive");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting stops.txt from zip file");
            throw;
        }
    }

    private List<Stop> ParseStopsTxt(string filePath)
    {
        var stops = new List<Stop>();
        if (!File.Exists(filePath))
        {
            _logger.LogError("File not found: {FilePath}", filePath);
            return stops;
        }

        try
        {
            var lines = File.ReadAllLines(filePath);
            _logger.LogInformation("Parsing {LineCount} lines from stops.txt", lines.Length);

            if (lines.Length == 0)
            {
                _logger.LogWarning("stops.txt file is empty");
                return stops;
            }

            // Log the header line for debugging
            _logger.LogInformation("Header line: {Header}", lines[0]);

            foreach (var line in lines.Skip(1)) // Skip header
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = line.Split(',');

                if (fields.Length < 4)
                {
                    _logger.LogWarning("Skipping line with insufficient fields: {Line}", line);
                    continue;
                }

                try
                {
                    stops.Add(new Stop
                    {
                        StopId = fields[0]?.Trim('"') ?? string.Empty,
                        StopName = fields[1]?.Trim('"') ?? string.Empty,
                        StopLat = double.TryParse(fields[2]?.Trim('"'), NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) ? lat : 0,
                        StopLon = double.TryParse(fields[3]?.Trim('"'), NumberStyles.Any, CultureInfo.InvariantCulture, out var lon) ? lon : 0,
                        StopDesc = fields.Length > 4 ? fields[4]?.Trim('"') : null,
                        LocationType = fields.Length > 5 ? fields[5]?.Trim('"') : null,
                        ParentStation = fields.Length > 6 ? fields[6]?.Trim('"') : null,
                        WheelchairBoarding = fields.Length > 7 ? fields[7]?.Trim('"') : null,
                        VehicleType = fields.Length > 8 ? fields[8]?.Trim('"') : null,
                        PlatformCode = fields.Length > 9 ? fields[9]?.Trim('"') : null
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing line: {Line}", line);
                }
            }

            _logger.LogInformation("Successfully parsed {StopCount} stops", stops.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading or parsing stops.txt file");
        }

        return stops;
    }
}