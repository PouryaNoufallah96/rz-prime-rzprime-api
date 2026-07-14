using RZPrime.Services._TransactionLog.DTOs.Updates;
using System.Numerics;

namespace RZPrime.Services._TransactionLog
{
    public interface ITransactionLogService
    {

        Task CreateOrderRegisteredTransactionLogAsync(OrderRegisteredLogData log);
        Task CreateOrderExecutedTransactionLogAsync(OrderExecutedLogData log);
        Task CreateOrderExpiredTransactionLogAsync(OrderExpiredLogData log);
        Task<BigInteger> GetLastCheckedBlockNumberAsync();
     
    }
}
