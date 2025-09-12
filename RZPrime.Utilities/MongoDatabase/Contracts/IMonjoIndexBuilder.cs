using MongoDB.Driver;
using System.Linq.Expressions;

namespace RZPrime.Utilities.MongoDatabase.Contracts
{
    public interface IMonjoIndexBuilder<TDocument>
    {
        IMonjoIndexBuilder<TDocument> AscendingIndex(Expression<Func<TDocument, object>> filterExpression);

        IMonjoIndexBuilder<TDocument> DescendingIndex(Expression<Func<TDocument, object>> filterExpression);

        void Build(CreateIndexOptions options = null);

        Task BuildAsync(CreateIndexOptions options = null);
    }
}
