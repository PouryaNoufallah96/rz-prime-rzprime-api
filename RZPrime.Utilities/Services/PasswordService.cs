using RZPrime.Utilities.Services.Contracts;
using RZPrime.Utilities.Utilities;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Utilities.Services
{
    public class PasswordService : IPasswordService, ISingletonDependency
    {
        public string Hash(string password)
            => BCrypt.Net.BCrypt.HashPassword(password);

        public bool Verify(string password, string passwordHash)
        {
            password.NotNull(nameof(password));
            passwordHash.NotNull(nameof(password));
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
    }
}