namespace JobHunter.Application.Common;

/// <summary>Ciphertext (with its tag) and the nonce it was made with.</summary>
public sealed record EncryptedSecret(byte[] Ciphertext, byte[] Nonce);

/// <summary>
/// Envelope encryption for secrets at rest (spec 02 §5): a per-row data key, wrapped by a
/// versioned key-encryption key from configuration, encrypts each secret with AES-256-GCM,
/// bound to the row's id so a ciphertext copied to another row fails to decrypt.
/// </summary>
public interface ISecretCipher
{
    (byte[] WrappedDataKey, int KeyVersion) CreateDataKey();

    EncryptedSecret Encrypt(byte[] wrappedDataKey, int keyVersion, string plaintext, Guid rowId);

    string Decrypt(byte[] wrappedDataKey, int keyVersion, EncryptedSecret secret, Guid rowId);
}
