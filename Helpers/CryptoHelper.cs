using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MediaDownloader.Helpers
{
    public static class CryptoHelper
    {
        // Chave AES de 256 bits (32 caracteres UTF-8)
        public const string DefaultKey = "YtDlpFfmpegKey2026SecureStr0000!";
        
        // IV de 128 bits (16 caracteres UTF-8)
        public const string DefaultIv = "InitVector123456";

        public static string Encrypt(string plainText, string key = DefaultKey, string iv = DefaultIv)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] ivBytes = Encoding.UTF8.GetBytes(iv);

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(encryptedBytes);
        }

        public static string Decrypt(string cipherText, string key = DefaultKey, string iv = DefaultIv)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] ivBytes = Encoding.UTF8.GetBytes(iv);

            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(decryptedBytes);
        }
    }
}

