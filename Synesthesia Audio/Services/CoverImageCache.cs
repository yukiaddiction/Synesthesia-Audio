using Microsoft.Maui.Controls;

namespace Synesthesia_Audio.Services;

/// <summary>Small LRU of cover ImageSources so scrolling lists don't re-decode the same artwork.</summary>
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

            // Guarded on the access list too: if it ever disagrees with the dictionary, this must not spin forever on the UI thread.
            while (_cache.Count >= MaxCacheSize && _accessOrder.Last != null)
            {
                var last = _accessOrder.Last;
                _cache.Remove(last.Value);
                _accessOrder.RemoveLast();
            }

            if (_cache.Count >= MaxCacheSize)
            {
                _cache.Clear();
                _accessOrder.Clear();
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
