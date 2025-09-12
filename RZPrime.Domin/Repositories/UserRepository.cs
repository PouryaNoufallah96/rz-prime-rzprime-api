using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories.Contracts;
using RZPrime.Utilities.MongoDatabase;
using RZPrime.Utilities.MongoDatabase.Contracts;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Domain.Repositories
{
    public class UserRepository(IMonjoConnection connection) 
        : MonjoRepository<User>(connection), IUserRepository, ISingletonDependency
    {
        protected override void Configure()
        {
            if (!CollectionExists())
            {
                AscendingIndex(q => q.WalletAddress).Build();
                AscendingIndex(q => q.UserPublicKey).Build();
            }
        }
    }
}
