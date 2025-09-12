using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories.Contracts;
using static RZPrime.Utilities.Constants.RegisterMode;
using RZPrime.Utilities.MongoDatabase;
using RZPrime.Utilities.MongoDatabase.Contracts;

namespace RZPrime.Domain.Repositories
{
    public class RequestLogRepository(IMonjoConnection connection)
        : MonjoRepository<RequestLog>(connection), IRequestLogRepository, ISingletonDependency
    {
    }
}
