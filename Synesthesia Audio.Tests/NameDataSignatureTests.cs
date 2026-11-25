using System.Security.Cryptography;
using System.Text;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// SEC-M-04 part 3: the published name-data manifest carries the data's SHA-256, so a hash check alone only
/// proves the data matches the manifest. An attacker who can replace the release assets replaces both. The
/// signature is what breaks that chain.
///
/// These tests generate a throwaway keypair at run time — no secret is committed, and nothing here depends on
/// a key existing in the repository.
/// </summary>
public sealed class NameDataSignatureTests
{
    private const string Manifest = """
        {
          "created": "2026-11-25",
          "count": 100,
          "sha256": "abc123"
        }
        """;

    private static ECDsa NewKeyPair() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private static string Sign(ECDsa key, string payload) =>
        Convert.ToBase64String(key.SignData(
            Encoding.UTF8.GetBytes(payload),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence));

    private static ECDsa? KeyFromPem(ECDsa key) => NameDataSignature.TryLoadPublicKey(key.ExportSubjectPublicKeyInfoPem());

    [Fact]
    public void A_signature_made_with_the_matching_private_key_is_accepted()
    {
        using var key = NewKeyPair();
        var signature = Sign(key, Manifest);

        Assert.True(NameDataSignature.IsValid(KeyFromPem(key), Encoding.UTF8.GetBytes(Manifest), signature));
    }

    /// <summary>The whole point: a swapped manifest must fail against the shipped public key.</summary>
    [Fact]
    public void A_tampered_manifest_is_rejected()
    {
        using var key = NewKeyPair();
        var signature = Sign(key, Manifest);

        var tampered = Manifest.Replace("abc123", "ffffff", StringComparison.Ordinal);

        Assert.False(NameDataSignature.IsValid(KeyFromPem(key), Encoding.UTF8.GetBytes(tampered), signature));
    }

    [Fact]
    public void A_signature_from_a_different_key_is_rejected()
    {
        using var attacker = NewKeyPair();
        using var project = NewKeyPair();

        var attackerSignature = Sign(attacker, Manifest);

        Assert.False(NameDataSignature.IsValid(KeyFromPem(project), Encoding.UTF8.GetBytes(Manifest), attackerSignature));
    }

    [Fact]
    public void A_missing_or_malformed_signature_is_rejected()
    {
        using var key = NewKeyPair();
        var payload = Encoding.UTF8.GetBytes(Manifest);

        Assert.False(NameDataSignature.IsValid(KeyFromPem(key), payload, null));
        Assert.False(NameDataSignature.IsValid(KeyFromPem(key), payload, ""));
        Assert.False(NameDataSignature.IsValid(KeyFromPem(key), payload, "not base64 !!"));
    }

    /// <summary>"Signing not configured" must never read as "verified".</summary>
    [Fact]
    public void A_missing_key_verifies_nothing()
    {
        using var key = NewKeyPair();

        Assert.Null(NameDataSignature.TryLoadPublicKey(null));
        Assert.Null(NameDataSignature.TryLoadPublicKey("  "));
        Assert.Null(NameDataSignature.TryLoadPublicKey("this is not a key"));

        Assert.False(NameDataSignature.IsValid(null, Encoding.UTF8.GetBytes(Manifest), Sign(key, Manifest)));
    }

    /// <summary>The workflow may deliver the public key as raw DER in base64 rather than PEM.</summary>
    [Fact]
    public void A_raw_der_public_key_is_also_accepted()
    {
        using var key = NewKeyPair();
        var derBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var signature = Sign(key, Manifest);

        var loaded = NameDataSignature.TryLoadPublicKey(derBase64);

        Assert.NotNull(loaded);
        Assert.True(NameDataSignature.IsValid(loaded, Encoding.UTF8.GetBytes(Manifest), signature));
    }

    /// <summary>Base64 signatures are frequently wrapped; the wrapper must not matter.</summary>
    [Fact]
    public void A_line_wrapped_signature_is_accepted()
    {
        using var key = NewKeyPair();
        var signature = Sign(key, Manifest);

        var wrapped = string.Join("\n", Chunk(signature, 64));

        Assert.True(NameDataSignature.IsValid(KeyFromPem(key), Encoding.UTF8.GetBytes(Manifest), wrapped));
    }

    private static IEnumerable<string> Chunk(string value, int size)
    {
        for (var i = 0; i < value.Length; i += size)
        {
            yield return value.Substring(i, Math.Min(size, value.Length - i));
        }
    }
}
