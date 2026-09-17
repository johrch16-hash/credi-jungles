using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace PrestamosCobros.Infrastructure.Security;

public class CifradoService : ICifradoService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public CifradoService(IConfiguration configuration)
    {
        var keyStr = configuration["Encryption:Key"];
        if (string.IsNullOrEmpty(keyStr))
        {
            keyStr = "dzzeHUb7lU2NqkC/zyA9W6nOLMO0PhXAeUT1XdTr7UY=";
        }

        var ivStr = configuration["Encryption:IV"];
        if (string.IsNullOrEmpty(ivStr))
        {
            ivStr = "NprlcapMYHyifRg1VhqnwA==";
        }

        _key = Convert.FromBase64String(keyStr);
        _iv = Convert.FromBase64String(ivStr);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        
        var iv = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(iv);
        }
        aes.IV = iv;
        
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        return Convert.ToBase64String(iv) + ":" + Convert.ToBase64String(cipherBytes);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        var parts = cipherText.Split(':');
        byte[] iv;
        byte[] cipherBytes;

        if (parts.Length == 2)
        {
            iv = Convert.FromBase64String(parts[0]);
            cipherBytes = Convert.FromBase64String(parts[1]);
        }
        else
        {
            // Fallback for older static IV encrypted data
            iv = _iv;
            cipherBytes = Convert.FromBase64String(cipherText);
        }

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public string ComputeHash(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        using var hmac = new HMACSHA256(_key);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(plainText));
        return Convert.ToBase64String(hashBytes);
    }
}
