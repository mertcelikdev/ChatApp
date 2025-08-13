using System.Security.Cryptography;
using System.Text;

namespace ChatApp.Services;

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    string HashPassword(string password);
}

public class EncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public EncryptionService(IConfiguration configuration)
    {
        // Sabit anahtar ve IV kullan (32 byte key, 16 byte IV)
        var keyString = "MERT_CHAT_APP_ENCRYPTION_KEY_32B"; // Tam 32 karakter
        var ivString = "CHAT_APP_IV_16B_"; // Tam 16 karakter
        
        _key = System.Text.Encoding.UTF8.GetBytes(keyString);
        _iv = System.Text.Encoding.UTF8.GetBytes(ivString);
        
        Console.WriteLine($"🔑 Key length: {_key.Length}, IV length: {_iv.Length}");
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var encryptor = aes.CreateEncryptor();
        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        using var swEncrypt = new StreamWriter(csEncrypt);
        
        swEncrypt.Write(plainText);
        swEncrypt.Close();
        
        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            var cipherBytes = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(cipherBytes);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var srDecrypt = new StreamReader(csDecrypt);
            
            return srDecrypt.ReadToEnd();
        }
        catch
        {
            // Şifre çözme hatası durumunda boş string döndür
            return string.Empty;
        }
    }

    public string HashPassword(string password)
    {
        // BCrypt kullanarak güvenli password hash'leme
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    // Yardımcı metot: Rastgele key ve IV oluşturma (sadece setup için)
    public static (string Key, string IV) GenerateKeyAndIV()
    {
        using var aes = Aes.Create();
        aes.GenerateKey();
        aes.GenerateIV();
        
        return (Convert.ToBase64String(aes.Key), Convert.ToBase64String(aes.IV));
    }
}
