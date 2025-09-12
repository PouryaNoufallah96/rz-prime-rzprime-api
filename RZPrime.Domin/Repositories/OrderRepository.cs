using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories.Contracts;
using RZPrime.Utilities.MongoDatabase;
using RZPrime.Utilities.MongoDatabase.Contracts;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Domain.Repositories
{
    public class OrderRepository(IMonjoConnection connection) : MonjoRepository<Order>(connection), IOrderRepository, ISingletonDependency
    {
        protected override void Configure()
        {
            if (!CollectionExists())
            {
                AscendingIndex(q => q.OrderId).Build();
                AscendingIndex(q => q.WalletAddress).Build();
                AscendingIndex(q => q.UserPublicKey).Build();
                AscendingIndex(q => q.State).Build();               
                AscendingIndex(q => q.TokenName).Build();               
            }
        }
    }
}
