using RZPrime.Domain.Collections;
using RZPrime.Services._Price.DTOs.Results;

namespace RZPrime.Services._Price
{
    public interface IPriceService
    {
        Task FetchAllPricesAsync();
        Task<Dictionary<string, PriceResult>> FetchAllPricesForInternalUsageAsync();
        Task<EffectivePriceResult> CalculateEffectivePriceAsync(string tokenName, decimal assetQuantity, decimal USDTAmount);
        Task<decimal> GetRZUSDPriceAsync();

        //Task<EffectivePriceResult> CalculateEffectivePriceAsync(string tokenName ,decimal assetQuantity);
        //Task<EffectivePriceResult> CalculateEffectivePriceForLandingAsync(string tokenName, decimal assetQuantity);
    }
} 
