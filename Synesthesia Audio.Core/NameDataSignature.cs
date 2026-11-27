using System.Security.Cryptography;

namespace Synesthesia_Audio.Core;

/// <summary>
/// The manifest's SHA-256 only proves data matches manifest; an attacker replacing release assets replaces both.
/// An ECDSA P-256 signature from an off-repo private key, checked against a compiled-in public key, breaks that chain.
/// </summary>
public static class NameDataSignature
{
    /// <summary>
    /// Accepts PEM or base64 DER. Null means signing is not configured, which callers must treat as "do not
    /// enforce", never as "verified".
    /// </summary>
    public static ECDsa? TryLoadPublicKey(string? keyMaterial)
    {
        if (string.IsNullOrWhiteSpace(keyMaterial)) return null;

        try
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(keyMaterial);
            return ecdsa;
        }
        catch (Exception)
        {
        }

        // Raw DER, as "openssl pkey -pubout -outform DER" writes it.
        try
        {
            var der = Convert.FromBase64String(StripWhitespace(keyMaterial));
            var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(der, out _);
            return ecdsa;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <param name="payload">The exact bytes that were signed, i.e. the manifest as published.</param>
    public static bool IsValid(ECDsa? publicKey, ReadOnlySpan<byte> payload, string? base64Signature)
    {
        if (publicKey is null) return false;
        if (string.IsNullOrWhiteSpace(base64Signature)) return false;

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(StripWhitespace(base64Signature));
        }
        catch (FormatException)
        {
            return false;
        }

        // OpenSSL writes DER-encoded signatures, not the fixed-size IEEE P1363 form .NET defaults to.
        return publicKey.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    }

    private static string StripWhitespace(string value)
    {
        return value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();
    }
}
