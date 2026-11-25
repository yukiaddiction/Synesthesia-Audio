using System.Security.Cryptography;

namespace Synesthesia_Audio.Core;

/// <summary>
/// Verifies that a published name-data manifest really came from this project.
///
/// The manifest carries the SHA-256 of the data, so a hash check alone only proves the data matches the
/// manifest — an attacker who can replace the release assets replaces both. The signature is what breaks that
/// chain: it is produced with a private key held outside the repository and verified with a public key
/// compiled into the app, so a swapped manifest is rejected.
///
/// ECDSA P-256 with SHA-256, signature in DER form (what <c>openssl dgst -sha256 -sign</c> produces). .NET
/// verifies that without any extra package, which keeps the dependency surface at zero.
/// </summary>
public static class NameDataSignature
{
    /// <summary>
    /// Loads a SubjectPublicKeyInfo (PEM or raw DER) public key, or returns null when the key is absent or
    /// unusable. A null key means "signing is not configured", which callers must treat as "do not enforce",
    /// not as "verified".
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
            // Not PEM; fall through to the raw-DER attempt below.
        }

        // Not PEM: try the raw DER that "openssl pkey -pubout -outform DER" writes.
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

    /// <summary>
    /// Verifies <paramref name="payload"/> against a base64 DER signature.
    /// </summary>
    /// <param name="publicKey">The key from <see cref="TryLoadPublicKey"/>.</param>
    /// <param name="payload">The exact bytes that were signed (the manifest as published).</param>
    /// <param name="base64Signature">The detached signature, base64 encoded.</param>
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

        // Openssl writes DER-encoded ECDSA signatures, not the fixed-size IEEE P1363 form.
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
