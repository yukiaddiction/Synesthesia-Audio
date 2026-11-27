using GTranslate.Translators;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// Free fallback chain: Google, Google's second endpoint, then Bing. Translators that need account
/// credentials are left out so the fallback stays anonymous.
/// </summary>
public sealed class GTranslateProvider : ITranslationProvider
{
    private readonly AggregateTranslator _translator;
    private readonly RetryAfterState _retryAfterState;
    private readonly ILogger _logger;

    public string Name => "GTranslate";

    public GTranslateProvider(
        HttpClient httpClient,
        RetryAfterState retryAfterState,
        ILogger logger)
    {
        _retryAfterState = retryAfterState;
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
            _retryAfterState.Reset();

            // One newline-joined request instead of one per string; inputs are flattened first so an embedded
            // newline cannot fake an extra item.
            var sanitized = texts.Select(BatchTranslationSplitter.SanitizeInput).ToList();
            var combined = string.Join("\n", sanitized);

            var result = await _translator
                .TranslateAsync(combined, "en", "ja")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            // Only an exact line count is trusted; a misaligned reply would attach one work's tag to another.
            var lines = BatchTranslationSplitter.TrySplit(result.Translation, texts.Count);

            if (lines != null) return lines.ToList();

            _logger.LogDebug("GTranslate batch could not be aligned to {Expected} items, falling back to individual",
                texts.Count);

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
            throw new TranslationRateLimitedException(Name, _retryAfterState.Last);
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
