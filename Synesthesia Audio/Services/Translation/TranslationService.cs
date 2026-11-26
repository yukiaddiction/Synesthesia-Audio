using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;
using Synesthesia_Audio.Services.Translation;

namespace Synesthesia_Audio.Services;

/// <summary>
/// Owns provider selection, the shared rate-limit cooldown and the user-facing state, so every
/// provider inherits the same "wait a little" behaviour instead of each rediscovering it.
/// </summary>
public sealed class TranslationService : ITranslationService
{
    private readonly SettingsService _settingsService;
    private readonly ILogger<TranslationService> _logger;
    private readonly RetryAfterState _retryAfterState;
    private readonly HttpClient _httpClient;
    // One object owns the cooldown clock and the backoff step, so they cannot drift apart (OVR-04).
    private readonly RateLimiter _limiter = new(
        TimeSpan.FromSeconds(AppConstants.TranslationRateLimitBaseCooldownSeconds),
        TimeSpan.FromSeconds(AppConstants.TranslationRateLimitMaxCooldownSeconds));

    private GTranslateProvider? _gTranslateProvider;

    private string? _lastError;

    public string? LastError => Volatile.Read(ref _lastError);

    public void ClearError() => Volatile.Write(ref _lastError, null);

    public TranslationService(SettingsService settingsService, IHttpClientFactory httpClientFactory, RetryAfterState retryAfterState, ILogger<TranslationService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
        _retryAfterState = retryAfterState;

        // Timeout and the Retry-After handler come from the named registration (DUP-02 / OVR-05).
        _httpClient = httpClientFactory.CreateClient(HttpClients.Translation);
    }

    public bool ContainsJapanese(string text) =>
        !string.IsNullOrEmpty(text) && AppConstants.JapaneseTextRegex.IsMatch(text);

    public string ToRomaji(string japaneseText) => RomajiTransliterator.ToRomaji(japaneseText);

    public string ActiveProviderName =>
        ResolveProviderKind(_settingsService.CurrentSettings.TranslationProvider);

    public TimeSpan? RateLimitRemaining
    {
        get
        {
            var remaining = _limiter.Remaining(DateTimeOffset.UtcNow);
            return remaining > TimeSpan.Zero ? remaining : null;
        }
    }

    public async Task<string> TranslateJaToEnAsync(string japaneseText, TranslationConfig? config = null)
    {
        if (string.IsNullOrWhiteSpace(japaneseText)) return string.Empty;
        if (!ContainsJapanese(japaneseText)) return string.Empty;

        var translated = await TranslateJaToEnBatchAsync(new[] { japaneseText }, config).ConfigureAwait(false);
        return translated.Count > 0 ? translated[0] : string.Empty;
    }

    public async Task<IReadOnlyList<string>> TranslateJaToEnBatchAsync(IReadOnlyList<string> japaneseTexts, TranslationConfig? config = null)
    {
        var results = new string[japaneseTexts.Count];

        var indices = new List<int>();
        var texts = new List<string>();
        for (var i = 0; i < japaneseTexts.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(japaneseTexts[i]) && ContainsJapanese(japaneseTexts[i]))
            {
                indices.Add(i);
                texts.Add(japaneseTexts[i]);
            }
        }

        if (texts.Count == 0) return results;

        // Still cooling down: spend no request at all rather than provoking another 429.
        if (_limiter.IsBlocked(DateTimeOffset.UtcNow))
        {
            _logger.LogDebug("Translation skipped — cooling down for another {Seconds:0}s",
                _limiter.Remaining(DateTimeOffset.UtcNow).TotalSeconds);
            return results;
        }

        var provider = CreateProvider(config);

        IReadOnlyList<string>? translated;
        try
        {
            translated = await provider.TranslateBatchAsync(texts, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TranslationRateLimitedException limited)
        {
            EnterCooldown(limited);
            return results;
        }

        if (translated == null || translated.Count != texts.Count)
        {
            Volatile.Write(ref _lastError, $"{provider.Name} could not translate right now. Please try again later.");
            _logger.LogWarning("{Provider} returned no usable translation for {Count} strings", provider.Name, texts.Count);
            return results;
        }

        _limiter.RecordSuccess();
        Volatile.Write(ref _lastError, null);

        for (var i = 0; i < indices.Count; i++)
            results[indices[i]] = translated[i];

        return results;
    }

    private void EnterCooldown(TranslationRateLimitedException limited)
    {
        var delay = _limiter.RecordRateLimit(DateTimeOffset.UtcNow, limited.RetryAfter);

        // Rate limiting is not an error: the UI shows a countdown instead of a failure message.
        Volatile.Write(ref _lastError, null);
        _logger.LogWarning("Translation rate limited by {Provider}; cooling down for {Seconds:0}s",
            limited.ProviderName, delay.TotalSeconds);
    }

    private ITranslationProvider CreateProvider(TranslationConfig? config = null)
    {
        var settings = config ?? TranslationConfig.FromSettings(_settingsService.CurrentSettings);
        var kind = ResolveProviderKind(settings.Provider);

        switch (kind)
        {
            case TranslationProviders.DeepL:
                return new DeepLProvider(
                    _httpClient,
                    _logger,
                    TranslationProviders.DeepL,
                    settings.ApiKey,
                    string.Empty,
                    "Authorization",
                    "DeepL-Auth-Key");

            case TranslationProviders.Custom:
                return new DeepLProvider(
                    _httpClient,
                    _logger,
                    TranslationProviders.Custom,
                    settings.ApiKey,
                    settings.CustomBaseUrl,
                    settings.CustomAuthHeader,
                    settings.CustomAuthScheme);

            default:
                return _gTranslateProvider ??= new GTranslateProvider(_httpClient, _retryAfterState, _logger);
        }
    }

    private static string ResolveProviderKind(string? configured) => configured switch
    {
        TranslationProviders.DeepL => TranslationProviders.DeepL,
        TranslationProviders.Custom => TranslationProviders.Custom,
        _ => TranslationProviders.GTranslate
    };
}
