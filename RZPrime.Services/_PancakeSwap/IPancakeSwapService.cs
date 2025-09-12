using RZPrime.Services._PancakeSwap.DTOs;

namespace RZPrime.Services._PancakeSwap
{
    public interface IPancakeSwapService
    {
        Task<decimal> GetMultiCallOptimalSwapAmountInBSCAsync(GetSwapAmountUpdate update);
        Task<decimal> GetOptimalSwapAmountInBSCAsync(GetSwapAmountUpdate update);
        Task<decimal> GetSwapAmountInBSCAsync(GetSwapAmountUpdate update);
        //Task<decimal> GetBestQuoteAsync(GetSwapAmountUpdate update);
        //Task<decimal> GetTokenOutAmountAsync(decimal amountIn, string tokenIn, string tokenOut, int decimalsIn = 18);
    }
}
 