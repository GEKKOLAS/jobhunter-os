using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using JobHunter.Application.Common;
using Microsoft.Extensions.Options;

namespace JobHunter.Infrastructure.Security;

public sealed class SecretsOptions
{
    public const string SectionName = "Secrets";

    /// <summary>
    /// The development key in appsettings.Development.json. Anything but Development refuses
    /// to start with it: a key in a repository protects nothing.
    /// </summary>
    public const string DevelopmentKey = "kZ0qJ6m1R0yFh4c8vH2xQ1n5T7w9Ab3dE6gJ8pLs0Uo=";

    /// <summary>Key-encryption keys by version, each 32 bytes in base64. Old versions stay to decrypt old rows.</summary>
    [Required, MinLength(1)]
    public Dictionary<int, string> Keys { get; init; } = [];

    [Range(1, int.MaxValue)]
    public int ActiveVersion { get; init; }

    public bool UsesDevelopmentKey => Keys.Values.Contains(DevelopmentKey);
}

/// <summary>
/// AES-256-GCM envelope encryption (spec 02 §5), ported from Mail Template Hub: a random data
/// key per row, wrapped by the active key-encryption key, encrypts the secret with the row's
/// id as associated data.
/// </summary>
public sealed class AesGcmSecretCipher : ISecretCipher
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly IReadOnlyDictionary<int, byte[]> _keys;
    private readonly int _active;

    public AesGcmSecretCipher(IOptions<SecretsOptions> options)
    {
        var value = options.Value;
        _keys = value.Keys.ToDictionary(kv => kv.Key, kv => Decode(kv.Key, kv.Value));
        if (!_keys.ContainsKey(value.ActiveVersion))
        {
            throw new InvalidOperationException($"Secrets:ActiveVersion {value.ActiveVersion} isn't one of Secrets:Keys.");
        }
        _active = value.ActiveVersion;
    }

    public (byte[] WrappedDataKey, int KeyVersion) CreateDataKey()
    {
        var dataKey = RandomNumberGenerator.GetBytes(KeySize);
        try
        {
            return (Wrap(dataKey, _active), _active);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public EncryptedSecret Encrypt(byte[] wrappedDataKey, int keyVersion, string plaintext, Guid rowId)
    {
        var dataKey = Unwrap(wrappedDataKey, keyVersion);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertext = new byte[bytes.Length];
            var tag = new byte[TagSize];
            using var aes = new AesGcm(dataKey, TagSize);
            aes.Encrypt(nonce, bytes, ciphertext, tag, rowId.ToByteArray());
            return new EncryptedSecret([.. ciphertext, .. tag], nonce);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public string Decrypt(byte[] wrappedDataKey, int keyVersion, EncryptedSecret secret, Guid rowId)
    {
        var dataKey = Unwrap(wrappedDataKey, keyVersion);
        try
        {
            var combined = secret.Ciphertext;
            var ciphertext = combined.AsSpan(0, combined.Length - TagSize);
            var tag = combined.AsSpan(combined.Length - TagSize);
            var plaintext = new byte[ciphertext.Length];
            using var aes = new AesGcm(dataKey, TagSize);
            aes.Decrypt(secret.Nonce, ciphertext, tag, plaintext, rowId.ToByteArray());
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private byte[] Wrap(byte[] dataKey, int version)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[dataKey.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_keys[version], TagSize);
        aes.Encrypt(nonce, dataKey, ciphertext, tag);
        return [.. nonce, .. ciphertext, .. tag];
    }

    private byte[] Unwrap(byte[] wrapped, int version)
    {
        if (!_keys.TryGetValue(version, out var key)) throw new InvalidOperationException($"Key version {version} isn't configured.");
        var nonce = wrapped.AsSpan(0, NonceSize);
        var tag = wrapped.AsSpan(wrapped.Length - TagSize);
        var ciphertext = wrapped.AsSpan(NonceSize, wrapped.Length - NonceSize - TagSize);
        var dataKey = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, dataKey);
        return dataKey;
    }

    private static byte[] Decode(int version, string base64)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Secrets key version {version} isn't valid base64.", ex);
        }
        return key.Length == KeySize
            ? key
            : throw new InvalidOperationException($"Secrets key version {version} must be {KeySize} bytes (got {key.Length}).");
    }
}
