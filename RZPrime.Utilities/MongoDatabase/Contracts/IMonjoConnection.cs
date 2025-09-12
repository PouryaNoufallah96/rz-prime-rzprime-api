using MongoDB.Driver;

namespace RZPrime.Utilities.MongoDatabase.Contracts
{
    public interface IMonjoConnection
    {
        IMongoClient Client { get; }
        IMongoDatabase Database { get; }
    }
}
