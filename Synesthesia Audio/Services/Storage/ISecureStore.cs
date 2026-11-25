namespace Synesthesia_Audio.Services.Storage;

/// <summary>
/// Credential store for user-supplied secrets (WebDAV password, translation API key).
/// Implementations must never write plaintext, and must never drop a value silently: when the
/// platform cannot persist, <see cref="LastError"/> says so and the value is kept only for the
/// lifetime of the process.
/// </summary>
public interface ISecureStore
{
    /// <summary>Set when the last read or write could not use durable storage. Cleared on success.</summary>
    string? LastError { get; }

    /// <summary>False when values are held in memory only, so the UI can warn the user.</summary>
    bool IsPersistent { get; }

    Task<string?> GetAsync(string key);
    Task<bool> SetAsync(string key, string value);
    Task RemoveAsync(string key);
}
