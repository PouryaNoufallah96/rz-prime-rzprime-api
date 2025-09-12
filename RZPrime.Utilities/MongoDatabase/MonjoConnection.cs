using MongoDB.Driver;
using static RZPrime.Utilities.Constants.RegisterMode;
using RZPrime.Utilities.MongoDatabase.Contracts;


namespace RZPrime.Utilities.MongoDatabase
{
    public class MonjoConnection : IMonjoConnection, ISingletonDependency
    {
        public IMongoClient Client { get; }
        public IMongoDatabase Database { get; }
        public MonjoConnection(IMonjoSettings settings)
        {
            Client = new MongoClient(settings.ConnectionString);
            Database = Client.GetDatabase(settings.DatabaseName);
        }
    }
}
