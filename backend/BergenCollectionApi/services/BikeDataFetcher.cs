using Microsoft.Extensions.Hosting;
using System.Text.Json;

public class BikeDataFetcher : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BikeDataCache _cache;
    private readonly ILogger<BikeDataFetcher> _logger;

    public BikeDataFetcher(IHttpClientFactory httpClientFactory, BikeDataCache cache, ILogger<BikeDataFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var stationStatUrl = "https://gbfs.urbansharing.com/bergenbysykkel.no/station_status.json";
                var stationInfoUrl = "https://gbfs.urbansharing.com/bergenbysykkel.no/station_information.json";

                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "BergenApp/1.0 (balx042025@gmail.com)");

                var statusTask = httpClient.GetStringAsync(stationStatUrl, stoppingToken);
                var infoTask = httpClient.GetStringAsync(stationInfoUrl, stoppingToken);

                await Task.WhenAll(statusTask, infoTask);

                var statusData = JsonSerializer.Deserialize<JsonElement>(statusTask.Result);
                var infoData = JsonSerializer.Deserialize<JsonElement>(infoTask.Result);

                var statusStations = statusData.GetProperty("data").GetProperty("stations").EnumerateArray();
                var infoStations = infoData.GetProperty("data").GetProperty("stations").EnumerateArray().ToList();

                var merged = statusStations.Select(station =>
                {
                    var stationId = station.GetProperty("station_id").GetString();
                    var matchingInfo = infoStations.FirstOrDefault(info =>
                        info.GetProperty("station_id").GetString() == stationId);

                    var result = new Dictionary<string, object>();

                    if (matchingInfo.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in matchingInfo.EnumerateObject())
                        {
                            result[prop.Name] = GetJsonValue(prop.Value);
                        }
                    }
                    foreach (var prop in station.EnumerateObject())
                    {
                        result[prop.Name] = GetJsonValue(prop.Value);
                    }
                    return result;
                }).ToList();

                _cache.Set(merged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating bike data cache");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private static object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }
}