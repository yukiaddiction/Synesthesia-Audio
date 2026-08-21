using Microsoft.Maui.Controls;

namespace Synesthesia_Audio.Services;

public class CoverImageCache
{
    private readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _accessOrder = new();
    private readonly object _lock = new();
    private const int MaxCacheSize = 50;

    public ImageSource? Get(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var source))
            {
                _accessOrder.Remove(path);
                _accessOrder.AddFirst(path);
                return source;
            }
        }
        return null;
    }

    public ImageSource GetOrCreate(string path)
    {
        var cached = Get(path);
        if (cached != null) return cached;

        var source = ImageSource.FromFile(path);
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var existing))
                return existing;

            while (_cache.Count >= MaxCacheSize)
            {
                var last = _accessOrder.Last;
                if (last != null)
                {
                    _cache.Remove(last.Value);
                    _accessOrder.RemoveLast();
                }
            }

            _cache[path] = source;
            _accessOrder.AddFirst(path);
        }
        return source;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _accessOrder.Clear();
        }
    }
}
