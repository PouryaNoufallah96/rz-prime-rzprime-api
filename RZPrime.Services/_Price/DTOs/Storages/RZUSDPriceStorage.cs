using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._Price.DTOs.Storages
{
    public class RZUSDPriceStorage : ISelfSingletonDependency
    {
        public decimal Price { get; set; }
        public DateTime LastUpdate { get; set; }

    }
}
