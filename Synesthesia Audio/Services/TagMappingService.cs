using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services;

public partial class TagMappingService
{
    private readonly ConcurrentDictionary<string, string> _tagMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonFileStore<Dictionary<string, string>> _store;
    private readonly ILogger<TagMappingService> _logger;

    // Matches any "[...]" or "(...)" annotation, lazily so adjacent groups are removed one by one.
    [System.Text.RegularExpressions.GeneratedRegex(@"\[.*?\]|\(.*?\)", System.Text.RegularExpressions.RegexOptions.None, 200)]
    private static partial System.Text.RegularExpressions.Regex BracketSuffixPattern();

    public TagMappingService(ILogger<TagMappingService> logger)
    {
        _logger = logger;
        _store = new JsonFileStore<Dictionary<string, string>>(
            Path.Combine(FileSystem.AppDataDirectory, "tag_mappings.json"),
            saveDebounceMs: 500);
        _store.LoadFailed += ex => _logger.LogError(ex, "Tag mappings could not be read — the previous file was kept as .bak");
        _store.SaveFailed += ex => _logger.LogError(ex, "SaveUserMappings failed");

        LoadInitialMappings();
        LoadUserMappings();

        if (!File.Exists(FilePath))
        {
            SaveUserMappings(force: true);
        }

        _logger.LogInformation("TagMappingService initialized — {Count} mappings loaded", _tagMap.Count);
    }

    private string FilePath => _store.Path;

    private void LoadInitialMappings()
    {
        try
        {
            var assembly = typeof(TagMappingService).Assembly;
            using var stream = assembly.GetManifestResourceStream("Synesthesia_Audio.Config.default_tag_mappings.json");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var defaults = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (defaults != null)
                {
                    foreach (var kvp in defaults)
                    {
                        _tagMap[kvp.Key] = kvp.Value;
                    }
                    _logger.LogDebug("Default tag mappings loaded — {Count} entries", defaults.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadInitialMappings failed");
        }
    }

    private void LoadUserMappings()
    {
        try
        {
            // Blocking on purpose: MapTag/MapGenres are synchronous, so the table must be complete before the
            // first call. The store has no thread-affine waits, so this cannot deadlock.
            var saved = _store.Loaded.GetAwaiter().GetResult();
            foreach (var kvp in saved)
            {
                if (!IsCorruptDictionaryArtifact(kvp.Value))
                {
                    _tagMap[kvp.Key] = kvp.Value;
                }
            }
            _logger.LogDebug("User tag mappings loaded — {Count} entries", saved.Count);
        }
        catch (Exception ex) { _logger.LogError(ex, "LoadUserMappings failed"); }
    }

    /// <summary>Dictionary-gloss leftovers (brackets, "allude"/"imply" usage notes) that aren't real tag names.</summary>
    public static bool IsCorruptDictionaryArtifact(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        return text.Contains("[") || text.Contains("]") || text.Contains("allude") || text.Contains("imply") || text.Contains("implication");
    }

    public static string SanitizeTranslation(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var clean = BracketSuffixPattern().Replace(raw, string.Empty).Trim();
        if (clean.Contains(","))
        {
            var parts = clean.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            clean = parts.FirstOrDefault(p => !p.Trim().Equals("t-", StringComparison.OrdinalIgnoreCase) &&
                                              !p.Trim().Equals("trans", StringComparison.OrdinalIgnoreCase) &&
                                              !p.Trim().Contains("imply") &&
                                              !p.Trim().Contains("allude"))?.Trim() ?? parts[0].Trim();
        }
        return string.IsNullOrWhiteSpace(clean) ? raw.Trim() : clean;
    }

    /// <summary>Debounced and atomic, because several DLsite tasks add mappings in parallel.</summary>
    private void SaveUserMappings(bool force = false)
    {
        _store.Replace(new Dictionary<string, string>(_tagMap, StringComparer.OrdinalIgnoreCase));
        _logger.LogDebug("Tag mappings save queued — {Count} entries", _tagMap.Count);

        if (force) FlushAsync().GetAwaiter().GetResult();
    }

    public Task FlushAsync() => _store.FlushAsync();

    public string MapTag(string rawTag)
    {
        if (string.IsNullOrWhiteSpace(rawTag)) return string.Empty;
        var trimmed = rawTag.Trim();
        if (_tagMap.TryGetValue(trimmed, out var english))
        {
            return english;
        }
        return trimmed;
    }

    public string MapGenres(string rawGenres)
    {
        if (string.IsNullOrWhiteSpace(rawGenres)) return string.Empty;
        var tags = rawGenres.Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries);
        var mapped = tags.Select(t => MapTag(t.Trim())).Where(t => !string.IsNullOrEmpty(t) && !IsCorruptDictionaryArtifact(t)).Distinct();
        return string.Join(", ", mapped);
    }

    public void AddOrUpdateMapping(string japaneseTag, string englishTag)
    {
        if (string.IsNullOrWhiteSpace(japaneseTag) || string.IsNullOrWhiteSpace(englishTag)) return;
        var cleanEng = SanitizeTranslation(englishTag);
        _tagMap[japaneseTag.Trim()] = cleanEng;
        _logger.LogInformation("Tag mapping added/updated — '{Japanese}' -> '{English}'", japaneseTag.Trim(), cleanEng);
        SaveUserMappings();
    }

    public void RemoveMapping(string japaneseTag)
    {
        if (string.IsNullOrWhiteSpace(japaneseTag)) return;
        _tagMap.TryRemove(japaneseTag.Trim(), out _);
        _logger.LogInformation("Tag mapping removed — '{Japanese}'", japaneseTag.Trim());
        SaveUserMappings();
    }

    public Dictionary<string, string> GetAllMappings()
    {
        return new Dictionary<string, string>(_tagMap, StringComparer.OrdinalIgnoreCase);
    }

    public void CleanBadMappings()
    {
        var badKeys = _tagMap.Where(kvp => IsCorruptDictionaryArtifact(kvp.Value) || IsCorruptDictionaryArtifact(kvp.Key)).Select(kvp => kvp.Key).ToList();
        foreach (var key in badKeys)
        {
            _tagMap.TryRemove(key, out _);
        }
        LoadInitialMappings();
        SaveUserMappings(force: true);
        _logger.LogInformation("Bad tag mappings cleaned — {Count} corrupt entries removed", badKeys.Count);
    }
}
