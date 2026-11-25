using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// A DeepL-API endpoint, also used for "Custom" so a user can point at their own DeepL-compatible
/// server (or any paid API that speaks the same JSON contract) without new code.
/// <para>
/// DeepL rejects a bare "EN" target, so EN-US is used. Authentication is the Authorization header
/// only — query-string auth_key was deprecated in March 2025 and removed by January 2026.
/// </para>
/// </summary>
public sealed class DeepLProvider : ITranslationProvider
{
    private const int MaxTextsPerRequest = 50;

    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _baseUrl;
    private readonly string _authHeaderName;
    private readonly string _authScheme;
    private readonly string _apiKey;

    public string Name { get; }

    /// <summary>Raised to the last Retry-After seen, so the shared cooldown can honour it.</summary>
    public TimeSpan? LastRetryAfter { get; private set; }

    public DeepLProvider(
        HttpClient httpClient,
        ILogger logger,
        string name,
        string apiKey,
        string baseUrl,
        string authHeaderName,
        string authScheme)
    {
        _httpClient = httpClient;
        _logger = logger;
        Name = name;
        _apiKey = apiKey;

        var defaultBaseUrl = DefaultBaseUrlFor(apiKey);
        // A custom endpoint receives the API key in a header, so an http:// base URL would leak it in the
        // clear. Only localhost may use http (SEC-m-04).
        if (!EndpointUrl.IsSecureOrLocal(baseUrl) && !string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("{Provider} base URL is not https and was ignored; using {Default}", name, defaultBaseUrl);
        }

        _baseUrl = EndpointUrl.UseOrFallback(baseUrl, defaultBaseUrl);
        _authHeaderName = string.IsNullOrWhiteSpace(authHeaderName) ? "Authorization" : authHeaderName;
        _authScheme = string.IsNullOrWhiteSpace(authScheme) ? "DeepL-Auth-Key" : authScheme;
    }

    public static string DefaultBaseUrlFor(string apiKey) =>
        apiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase)
            ? "https://api-free.deepl.com"
            : "https://api.deepl.com";

    public async Task<IReadOnlyList<string>?> TranslateBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        if (texts.Count == 0) return Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogDebug("{Provider} skipped: no API key configured", Name);
            return null;
        }

        LastRetryAfter = null;
        var translations = new List<string>(texts.Count);

        try
        {
            for (var offset = 0; offset < texts.Count; offset += MaxTextsPerRequest)
            {
                var chunk = texts.Skip(offset).Take(MaxTextsPerRequest).ToList();
                var translated = await TranslateChunkAsync(chunk, cancellationToken).ConfigureAwait(false);
                if (translated == null) return null;
                translations.AddRange(translated);
            }

            return translations;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Provider} translation failed", Name);
            return null;
        }
    }

    private async Task<List<string>?> TranslateChunkAsync(List<string> texts, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            text = texts,
            source_lang = "JA",
            target_lang = "EN-US"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v2/translate")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation(_authHeaderName, $"{_authScheme} {_apiKey}");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.TooManyRequests or (HttpStatusCode)529)
        {
            LastRetryAfter = ReadRetryAfter(response);
            throw new TranslationRateLimitedException(Name, LastRetryAfter);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("{Provider} returned HTTP {Status}", Name, (int)response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("translations", out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("{Provider} response had no 'translations' array", Name);
            return null;
        }

        var results = new List<string>(array.GetArrayLength());
        foreach (var entry in array.EnumerateArray())
        {
            results.Add(entry.TryGetProperty("text", out var text) ? text.GetString() ?? string.Empty : string.Empty);
        }

        return results.Count == texts.Count ? results : null;
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta) return delta;

        if (retryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) return wait;
        }

        return null;
    }
}
