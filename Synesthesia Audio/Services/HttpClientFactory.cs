using System.Net.Http;

namespace Synesthesia_Audio.Services;

public class HttpClientFactory
{
    private readonly Dictionary<string, HttpClient> _clients = new();

    public HttpClient GetOrCreate(string name, Func<HttpClient> factory)
    {
        if (!_clients.TryGetValue(name, out var client))
        {
            client = factory();
            _clients[name] = client;
        }
        return client;
    }
}
