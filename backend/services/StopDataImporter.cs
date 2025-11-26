using System.IO.Compression;
using System.Globalization;
using BergenCollectionApi.data;
using BergenCollectionApi.models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Npgsql;


public class StopDataImporter : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StopDataImporter> _logger;
    private readonly string _gtfsZipUrl = "https://storage.googleapis.com/marduk-production/outbound/gtfs/rb_sky-aggregated-gtfs.zip";
    private readonly string _baseDataPath;
    private readonly string _zipPath;
    private readonly string _extractPath;


    public StopDataImporter(IServiceProvider serviceProvider, ILogger<StopDataImporter> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Use an ephemeral, writable path (works locally and on platforms like Render)
        var root = Environment.GetEnvironmentVariable("DATA_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(Path.GetTempPath(), "bergenapp");
        }
        _baseDataPath = root;
        _zipPath = Path.Combine(_baseDataPath, "gtfs.zip");
        _extractPath = Path.Combine(_baseDataPath, "zipCache", "stops.txt");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StopsDbContext>();

        try
        {
            // Ensure the database itself exists (create DB if missing), then wait for readiness
            await EnsureDatabaseExistsAsync(dbContext, stoppingToken);
            await WaitForDatabaseAsync(dbContext, stoppingToken);

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
                _logger.LogInformation("Using data directory: {Directory}", zipDir);
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

    private async Task EnsureDatabaseExistsAsync(StopsDbContext db, CancellationToken ct)
    {
        // Build a maintenance connection (to 'postgres') so we can create the target database if missing
        var targetCs = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(targetCs))
        {
            _logger.LogWarning("No connection string available to ensure database exists.");
            return;
        }

        var target = new NpgsqlConnectionStringBuilder(targetCs);
        var targetDbName = target.Database;

        // If database name is empty, nothing we can do
        if (string.IsNullOrWhiteSpace(targetDbName))
        {
            _logger.LogWarning("No Database specified in connection string; skipping EnsureDatabaseExists.");
            return;
        }

        // First: try to connect to the target database; if it exists and is accessible, we're done.
        try
        {
            await using (var probe = new NpgsqlConnection(targetCs))
            {
                await probe.OpenAsync(ct);
                _logger.LogInformation("Database '{DbName}' is accessible.", targetDbName);
                return;
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "3D000")
        {
            // Database does not exist → attempt to create it using maintenance DB
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not connect to database '{DbName}' to probe existence; proceeding to creation attempt if permitted.", targetDbName);
        }

        var adminBuilder = new NpgsqlConnectionStringBuilder(targetCs)
        {
            Database = "postgres"
        };

        try
        {
            await using var adminConn = new NpgsqlConnection(adminBuilder.ConnectionString);
            await adminConn.OpenAsync(ct);

            await using (var existsCmd = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @name)", adminConn))
            {
                existsCmd.Parameters.AddWithValue("name", targetDbName);
                var exists = (bool)(await existsCmd.ExecuteScalarAsync(ct) ?? false);
                if (exists)
                {
                    _logger.LogInformation("Database '{DbName}' already exists.", targetDbName);
                    return;
                }
            }

            _logger.LogInformation("Database '{DbName}' not found. Creating...", targetDbName);
            // Quote identifier to be safe
            var quotedName = '"' + targetDbName.Replace("\"", "\"\"") + '"';
            await using (var createCmd = new NpgsqlCommand($"CREATE DATABASE {quotedName}", adminConn))
            {
                await createCmd.ExecuteNonQueryAsync(ct);
            }
            _logger.LogInformation("Database '{DbName}' created successfully.", targetDbName);
        }
        catch (PostgresException pex)
        {
            if (pex.SqlState == "42P04") // duplicate_database
            {
                _logger.LogWarning("Database '{DbName}' was created concurrently.", targetDbName);
                return;
            }
            // Some providers disallow connecting to 'postgres' or creating databases; log and continue
            _logger.LogWarning(pex, "Unable to create database '{DbName}'. It may require manual creation or provider permissions.", targetDbName);
        }
    }

    private async Task WaitForDatabaseAsync(StopsDbContext db, CancellationToken ct)
    {
        var attempt = 0;
        var maxAttempts = 10;
        var delay = TimeSpan.FromSeconds(1);

        while (!ct.IsCancellationRequested && attempt < maxAttempts)
        {
            try
            {
                if (await db.Database.CanConnectAsync(ct))
                {
                    _logger.LogInformation("Database connection established.");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database not ready yet (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}s...", attempt + 1, maxAttempts, delay.TotalSeconds);
            }

            attempt++;
            await Task.Delay(delay, ct);
            // Exponential backoff up to ~30s
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
        }

        // One last try to throw a more explicit error if still not reachable
        if (!await db.Database.CanConnectAsync(ct))
        {
            throw new InvalidOperationException("Could not connect to the PostgreSQL database after multiple attempts. Ensure the server is running and reachable.");
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