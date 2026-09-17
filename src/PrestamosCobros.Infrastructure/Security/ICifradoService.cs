namespace PrestamosCobros.Infrastructure.Security;

public interface ICifradoService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    string ComputeHash(string plainText);
}
