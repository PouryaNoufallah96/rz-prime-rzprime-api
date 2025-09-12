using RZPrime.Domain.Repositories.Contracts;
using static RZPrime.Utilities.Constants.RegisterMode;
using RZPrime.Domain.Collections;
using RZPrime.Utilities.MongoDatabase;
using RZPrime.Utilities.MongoDatabase.Contracts;

namespace RZPrime.Domain.Repositories
{
    public class LogRepository(IMonjoConnection connection) : MonjoRepository<Log>(connection), ILogRepository, ISingletonDependency
    {
    }
}
