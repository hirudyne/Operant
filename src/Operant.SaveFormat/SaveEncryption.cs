using System.Security.Cryptography;
using System.Text;

namespace Operant.SaveFormat;

/// <summary>
/// AES-256-CBC encryption with PBKDF2-HMAC-SHA1 key derivation, matching the
/// reverse-engineered <c>ZAUM.Save.Encryption.EncryptionUtility</c> in the
/// Zero Parades binary. Format on disk: <c>[32-byte salt][16-byte IV][cipher]</c>.
/// </summary>
public static class SaveEncryption
{
    /// <summary>
    /// Hardcoded seed from <c>ZAUM.C4.Save.Session.C4DataConverter</c>.
    /// </summary>
    public static readonly byte[] Seed = Encoding.ASCII.GetBytes("f52163935dc25a145a3eb5693a084752");

    public const int SaltSize = 32;
    public const int IvSize = 16;
    public const int KeySize = 32;
    public const int Pbkdf2Iterations = 1000;

    public static byte[] Decrypt(byte[] blob)
    {
        if (blob.Length < SaltSize + IvSize)
            throw new ArgumentException($"encrypted blob too short ({blob.Length} bytes)", nameof(blob));

        byte[] salt = blob[..SaltSize];
        byte[] iv = blob[SaltSize..(SaltSize + IvSize)];
        byte[] cipher = blob[(SaltSize + IvSize)..];

        using var aes = CreateAes(salt, iv);
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
    }

    public static byte[] Encrypt(byte[] plaintext, byte[]? salt = null, byte[]? iv = null)
    {
        salt ??= RandomNumberGenerator.GetBytes(SaltSize);
        iv ??= RandomNumberGenerator.GetBytes(IvSize);
        if (salt.Length != SaltSize)
            throw new ArgumentException($"salt must be {SaltSize} bytes", nameof(salt));
        if (iv.Length != IvSize)
            throw new ArgumentException($"iv must be {IvSize} bytes", nameof(iv));

        using var aes = CreateAes(salt, iv);
        using var encryptor = aes.CreateEncryptor();
        byte[] cipher = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        byte[] result = new byte[SaltSize + IvSize + cipher.Length];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(iv, 0, result, SaltSize, IvSize);
        Buffer.BlockCopy(cipher, 0, result, SaltSize + IvSize, cipher.Length);
        return result;
    }

    private static Aes CreateAes(byte[] salt, byte[] iv)
    {
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            password: Seed,
            salt: salt,
            iterations: Pbkdf2Iterations,
            hashAlgorithm: HashAlgorithmName.SHA1,
            outputLength: KeySize);

        var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;
        return aes;
    }
}
