using System.Security.Cryptography;
using System.Text;

namespace Synesthesia_Audio.Core;

/// <summary>
/// Guards the loopback streaming proxy, which any local process can reach. Generated per run, never logged, and
/// compared in constant time so it cannot be recovered byte by byte.
/// </summary>
public sealed class ProxyAccessToken
{
    private readonly string _text;

    private ProxyAccessToken(byte[] bytes)
    {
        _text = ToUrlSafeBase64(bytes);
    }

    public static ProxyAccessToken Create() => new(RandomNumberGenerator.GetBytes(32));

    /// <summary>For tests and for restoring a run's token.</summary>
    public static ProxyAccessToken FromBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0) throw new ArgumentException("A token needs at least one byte.", nameof(bytes));
        return new ProxyAccessToken(bytes);
    }

    public string Value => _text;

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
