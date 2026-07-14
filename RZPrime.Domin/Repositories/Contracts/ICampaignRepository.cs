using RZPrime.Domain.Collections;
using RZPrime.Utilities.MongoDatabase.Contracts;

namespace RZPrime.Domain.Repositories.Contracts
{
    public interface ICampaignRepository : IMonjoRepository<Campaign>
    {
    }
}
