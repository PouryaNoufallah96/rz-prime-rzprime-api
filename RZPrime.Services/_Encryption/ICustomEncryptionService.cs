namespace RZPrime.Services._Encryption
{
    public interface ICustomEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }
}
