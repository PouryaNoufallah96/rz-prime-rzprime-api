using MongoDB.Driver;
using RZPrime.Utilities.MongoDatabase.Extensions;
using RZPrime.Utilities.MongoDatabase.Filter;


namespace RZPrime.Utilities.MongoDatabase.Extensions
{
    public static class IFindFluetnExtentions
    {
        public static async Task<MonjoFilteredResult<T>> ExecuteAsync<T>(this IFindFluent<T, T> query, MonjoPage paging)
        {
            var findFluent = paging.Apply(query);

            var totalCount = await findFluent.CountDocumentsAsync();
            var pageSize = paging?.Size ?? totalCount;
            var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

            var data = await findFluent.ToListAsync();

            var result = new MonjoFilteredResult<T>
            {
                TotalCount = totalCount,
                PageCount = pageCount,
                Data = data
            };

            return result;
        }
    }
}
