using RZPrime.Services._PancakeSwap.DTOs;

namespace RZPrime.Services._PancakeSwap
{
    public interface IPancakeSwapService
    {
        Task<decimal> GetMultiCallOptimalSwapAmountInBSCAsync(GetSwapAmountUpdate update);
        Task<decimal> GetOptimalSwapAmountInBSCAsync(GetSwapAmountUpdate update);
      
    }
}
 