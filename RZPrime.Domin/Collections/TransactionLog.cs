using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.MongoDatabase.Documents;

namespace RZPrime.Domain.Collections
{
    [MonjoCollectionName("TransactionLogs")]
    public class TransactionLog : BaseDocument
    {
        public string TransactionLogId { get; set; } = Guid.NewGuid().ToString("N");
        public string OrderId { get; set; }
        public string UserWallet { get; set; }
        public string TokenName { get; set; } 
        public decimal TokenAmount { get; set; }
        public decimal USDTAmount { get; set; }
        public bool IsError { get; set; } = false;
        public List<TransactionLogHistory> Histories { get; set; } = [];
    }


    public class TransactionLogHistory
    {
        public DateTime CreateMoment { get; set; } = DateTime.UtcNow;
        public string From { get; set; }
        public string To { get; set; }
        public string Hash { get; set; }
        public long BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public TransactionStatus Status { get; set; }
        public decimal Amount { get; set; } 

    }

    public enum TransactionStatus
    {
        Pending,
        Confirmed,
        Failed
    }

    public enum BlockchainEventType
    {
        OrderRegistered,
        OrderExecuted,
        TransactionConfirmed,
        TransactionFailed,
        BlockMined, 
        NetworkStatus
    }
}
