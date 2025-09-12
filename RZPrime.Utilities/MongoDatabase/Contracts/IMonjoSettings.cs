namespace RZPrime.Utilities.MongoDatabase.Contracts
{
    public interface IMonjoSettings
    {
        string ConnectionString { get; set; }
        string DatabaseName { get; set; }
    }
}
