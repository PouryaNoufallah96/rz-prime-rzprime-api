
using Services._Price._RZPriceService.DTOs;

namespace Services._Price._RZPriceService
{
    public interface IRZPriceService
    {
        Task<RZPriceResult?> FetchTokenPriceAsync(string tokenName);

        Task<List<RZPriceResult>> FetchTokensPriceAsync(List<string> tokenNames);

        Task<CoinHistoryData?> FetchTokenHistoryAsync(string tokenName);
    }
}
