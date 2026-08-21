using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services;

public class TagMappingService
{
    private readonly ConcurrentDictionary<string, string> _tagMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _filePath;
    private readonly ILogger<TagMappingService> _logger;

    public TagMappingService(ILogger<TagMappingService> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "tag_mappings.json");
        LoadInitialMappings();
        LoadUserMappings();

        // Save permanent cache file on first launch if it doesn't exist yet
        if (!File.Exists(_filePath))
        {
            SaveUserMappings();
        }

        _logger.LogInformation("TagMappingService initialized — {Count} mappings loaded", _tagMap.Count);
    }

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
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (saved != null)
                {
                    foreach (var kvp in saved)
                    {
                        // Filter out corrupt dictionary artifacts automatically
                        if (!IsCorruptDictionaryArtifact(kvp.Value))
                        {
                            _tagMap[kvp.Key] = kvp.Value;
                        }
                    }
                    _logger.LogDebug("User tag mappings loaded — {Count} entries", saved.Count);
                }
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "LoadUserMappings failed"); }
    }

    public static bool IsCorruptDictionaryArtifact(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        return text.Contains("[") || text.Contains("]") || text.Contains("allude") || text.Contains("imply") || text.Contains("implication");
    }

    public static string SanitizeTranslation(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var clean = System.Text.RegularExpressions.Regex.Replace(raw, @"\[.*?\]|\(.*?\)", "").Trim();
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

    private void SaveUserMappings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_tagMap, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
            _logger.LogDebug("Tag mappings saved — {Count} entries", _tagMap.Count);
        }
        catch (Exception ex) { _logger.LogError(ex, "SaveUserMappings failed"); }
    }

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
        SaveUserMappings();
        _logger.LogInformation("Bad tag mappings cleaned — {Count} corrupt entries removed", badKeys.Count);
    }
}
