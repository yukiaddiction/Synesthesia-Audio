using System.Security.Cryptography;
using System.Text;

namespace Synesthesia_Audio.Core;

/// <summary>
/// The secret that guards the loopback streaming proxy.
///
/// A loopback port is reachable by any local process, so the proxy must prove the request came from us.
/// The token is generated per run, never written to a log, and compared in constant time so an attacker
/// cannot recover it a byte at a time.
/// </summary>
public sealed class ProxyAccessToken
{
    private readonly string _text;

    private ProxyAccessToken(byte[] bytes)
    {
        _text = ToUrlSafeBase64(bytes);
    }

    /// <summary>Creates a token from 32 random bytes.</summary>
    public static ProxyAccessToken Create() => new(RandomNumberGenerator.GetBytes(32));

    /// <summary>Recreates a token from known bytes. Intended for tests and for restoring a run's token.</summary>
    public static ProxyAccessToken FromBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0) throw new ArgumentException("A token needs at least one byte.", nameof(bytes));
        return new ProxyAccessToken(bytes);
    }

    /// <summary>The URL-safe form embedded in stream URLs.</summary>
    public string Value => _text;

    /// <summary>Constant-time comparison. Missing or differing values never match.</summary>
    public bool Matches(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate)) return false;

        var provided = Encoding.UTF8.GetBytes(candidate);
        var expected = Encoding.UTF8.GetBytes(_text);
        if (provided.Length != expected.Length) return false;

        return CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    private static string ToUrlSafeBase64(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
