using System.Text.Json;

public class BikeDataCache
{
    private List<Dictionary<string, object>> _cache = new();
    private readonly object _lock = new();

    public void Set(List<Dictionary<string, object>> data)
    {
        lock (_lock)
        {
            _cache = data;
        }
    }

    public List<Dictionary<string, object>> Get()
    {
        lock (_lock)
        {
            return _cache.ToList();
        }
    }
}