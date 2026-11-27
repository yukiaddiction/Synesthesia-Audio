namespace Synesthesia_Audio.Services.Storage;

/// <summary>
/// Store for user secrets. Implementations never write plaintext and never drop a value silently: when the platform
/// cannot persist, <see cref="LastError"/> says so and the value lives only for this process.
/// </summary>
public interface ISecureStore
{
    string? LastError { get; }

    // False when values are held in memory only, so the UI can warn the user.
    bool IsPersistent { get; }

    Task<string?> GetAsync(string key);
    Task<bool> SetAsync(string key, string value);
    Task RemoveAsync(string key);
}
