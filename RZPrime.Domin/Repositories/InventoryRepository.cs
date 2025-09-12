using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories.Contracts;
using RZPrime.Utilities.MongoDatabase;
using RZPrime.Utilities.MongoDatabase.Contracts;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Domain.Repositories
{
    public class InventoryRepository(IMonjoConnection connection) :
        MonjoRepository<Inventory>(connection), IInventoryRepository, ISingletonDependency
    {
        
    }
}
