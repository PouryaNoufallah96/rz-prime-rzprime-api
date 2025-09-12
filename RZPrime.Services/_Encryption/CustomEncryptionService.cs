using RZPrime.Services._Encryption.DTOs.Settings;
using RZPrime.Utilities.Exceptions.Common;
using System.Security.Cryptography;
using System.Text;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._Encryption
{
    public class CustomEncryptionService(CustomEncryptionSetting _customEncryptionSetting) :
        ICustomEncryptionService, ISingletonDependency
    {
        public string Encrypt(string plainText)
        {
            byte[] key = Encoding.UTF8.GetBytes(_customEncryptionSetting.Key);
            byte[] iv = Encoding.UTF8.GetBytes(_customEncryptionSetting.IV);

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(plainText);
                        }

                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
        }

        public string Decrypt(string cipherText)
        {
            try
            {


                byte[] key = Encoding.UTF8.GetBytes(_customEncryptionSetting.Key);
                byte[] iv = Encoding.UTF8.GetBytes(_customEncryptionSetting.IV);
                byte[] buffer = Convert.FromBase64String(cipherText);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (MemoryStream ms = new MemoryStream(buffer))
                    {
                        using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader sr = new StreamReader(cs))
                            {
                                return sr.ReadToEnd();
                            }
                        }
                    }
                }

            }
            catch (Exception)
            {

                throw new BadRequestException("bad header!");
            }
        }

    }
}
