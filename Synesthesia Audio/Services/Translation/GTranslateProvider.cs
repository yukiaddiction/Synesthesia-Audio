using GTranslate.Translators;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// The free translation chain, wrapped by GTranslate's AggregateTranslator: Google, then Google's
/// second endpoint, then Bing. Microsoft and Yandex are left out because they need account
/// credentials, which would turn an anonymous fallback into a signup requirement.
/// </summary>
public sealed class GTranslateProvider : ITranslationProvider
{
    private readonly AggregateTranslator _translator;
    private readonly RetryAfterCaptureHandler _retryAfterHandler;
    private readonly ILogger _logger;

    public string Name => "GTranslate";

    public GTranslateProvider(
        HttpClient httpClient,
        RetryAfterCaptureHandler retryAfterHandler,
        ILogger logger)
    {
        _retryAfterHandler = retryAfterHandler;
        _logger = logger;

        _translator = new AggregateTranslator(new ITranslator[]
        {
            new GoogleTranslator(httpClient),
            new GoogleTranslator2(httpClient),
            new BingTranslator(httpClient)
        });
    }

    public async Task<IReadOnlyList<string>?> TranslateBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        if (texts.Count == 0) return Array.Empty<string>();

        try
        {
            _retryAfterHandler.Reset();

            // Join on newlines so a list of tags costs one request instead of one per tag. When the
            // service reflows the lines the count no longer matches, so fall back to per-item calls.
            var combined = string.Join("\n", texts);
            var result = await _translator
                .TranslateAsync(combined, "en", "ja")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            var lines = result.Translation
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();

            if (lines.Count == texts.Count) return lines;

            _logger.LogDebug("GTranslate batch line count mismatch — expected {Expected}, got {Actual}, falling back to individual",
                texts.Count, lines.Count);

            var results = new string[texts.Count];
            for (var i = 0; i < texts.Count; i++)
            {
                var single = await _translator
                    .TranslateAsync(texts[i], "en", "ja")
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                results[i] = single.Translation;
            }

            return results;
        }
        catch (Exception ex) when (RateLimitDetector.IsRateLimit(ex))
        {
            throw new TranslationRateLimitedException(Name, _retryAfterHandler.LastRetryAfter);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GTranslate translation failed");
            return null;
        }
    }
}
