using RZPrime.Services._BlockChain.DTOs;
using RZPrime.Services._BlockChain.DTOs.Results;
using System.Numerics;
using RZPrime.Domain.Collections;

namespace RZPrime.Services._BlockChain
{
    public interface IBlockChainService
    {
        Task<TransactionResult> RegisterOrderOnBlockChainAsync(Order order);
        Task<string> SignDropOnBlockChainAsync(List<Order> ordersForDrop);

        Task<Dictionary<string, decimal>> GetContractBalancesAsync();
        Task<decimal> GetContractSingleBalanceAsync(string tokenName);
        Task<Dictionary<string, decimal>> GetBalancesMultiCallAsync();

        decimal ConvertFromWei(BigInteger weiAmount, int decimals = 18);
        BigInteger ConvertToWei(decimal amount, int decimals = 18);

        // Campaign methods
        Task<TransactionResult> CreateCampaignAsync(CreateCampaignOnBlockChainRequest request);
        Task<TransactionResult> EditCampaignAsync(CreateCampaignOnBlockChainRequest request);
        Task<TransactionResult> RemoveCampaignAsync(string campaignReference);
        Task<TransactionResult> SetDiscountForAsync(string walletAddress, decimal discountPercentage);
        Task<BigInteger> PreviewPaymentAmountAsync(string walletAddress, string orderId);
    }
}
