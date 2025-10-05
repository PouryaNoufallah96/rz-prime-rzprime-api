using RZPrime.Services._BlockChain.DTOs.Results;
using RZPrime.Services._BlockChain.DTOs;
using System.Numerics;
using RZPrime.Domain.Collections;
using Nethereum.RPC.Eth.DTOs;
using RZPrime.Services._BlockChainWebSocket.DTOs;

namespace RZPrime.Services._BlockChain
{
    public interface IBlockChainService
    {
        // Transactional Methods
        Task<TransactionResult> RegisterOrderOnBlockChainAsync(Order order);
        Task<TransactionResult> ExecuteOrderOnBlockchainAsync(string orderId);
        Task<List<(OrderExecutedEventDTO Event, FilterLog log, TransactionReceipt Transaction)>>  SyncExecutedOrdersDataWithOrderIdAsync(
           string userAddress,
           string orderId,
           BigInteger fromBlock = default);
          Task<(OrderExecutedEventDTO Event, FilterLog log, TransactionReceipt Transaction)?>  SyncExecutedOrderWithOrderIdAsync(
           string userAddress,
           string orderId,
           BigInteger fromBlock = default);

        Task<string> SignDropOnBlockChainAsync(List<Order> ordersForDrop);

        Task<Dictionary<string, decimal>> GetContractBalancesAsync();
        Task<decimal> GetContractSingleBalanceAsync(string tokenName);
        Task<Dictionary<string, decimal>> GetBalancesMultiCallAsync();

        // Query Methods
        Task<OrderDetails?> GetOrderFromBlockchainAsync(string userAddress, string orderId);
        Task<List<BatchOrderResultDto>> BatchGetOrdersAsync(List<(string userAddress, string orderId)> userOrders);


        // Network & Account Methods
        Task<AccountBalanceDto> GetAccountBalanceAsync(string? address = null);
        Task<NetworkStatusDto> GetNetworkStatusAsync();

        // Utility Methods
        decimal ConvertFromWei(BigInteger weiAmount, int decimals = 18);
        BigInteger ConvertToWei(decimal amount, int decimals = 18);

        Task PrintTransactionLogsAsync(string txHash);
        //Task PollMissingLogsAsync();
    }
}
