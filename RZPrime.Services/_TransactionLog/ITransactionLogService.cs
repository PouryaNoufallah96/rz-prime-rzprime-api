using RZPrime.Domain.Collections;
using RZPrime.Services._TransactionLog.DTOs;
using RZPrime.Services._TransactionLog.DTOs.Results;
using RZPrime.Utilities.DTOs;
using System.Numerics;

namespace RZPrime.Services._TransactionLog
{
    public interface ITransactionLogService
    {
        Task<TransactionListResult> ListTransactionsAsync(Pagination pagination, string walletAddress);
        Task CreateOrderRegisteredTransactionLogAsync(RegisteredTxLog log);
        Task CreateOrderExecutedTransactionLogAsync(ExecutedTxLog log);
        Task CreateOrderConfirmedTransactionLogAsync(ConfirmTxLog log);
        Task CreateOrderFailedTransactionLogAsync(FailTxLog log);
        Task<BigInteger> GetLastCheckedBlockNumberAsync();
        Task<TransactionLog> GetOneTransactionLogWithOrderIdAndWalletAsync(string orderId, string walletAddress);
        //Task<IEnumerable<RZPrime.Domain.Collections.TransactionLog>> GetByOrderIdAsync(string orderId);
    }
}
