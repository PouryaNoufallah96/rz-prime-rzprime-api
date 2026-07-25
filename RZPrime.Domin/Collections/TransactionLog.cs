using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.MongoDatabase.Documents;

namespace RZPrime.Domain.Collections
{
    [MonjoCollectionName("TransactionLogs")]
    public class TransactionLog : BaseDocument
    {
        public string TransactionLogId { get; set; } = Guid.NewGuid().ToString("N");
        public string OrderId { get; set; }
        public string Address { get; set; }
        public string UserWallet { get; set; }
        public string TokenName { get; set; } 
        public string TokenAmount { get; set; }
        public string USDTAmount { get; set; }
        public string Hash { get; set; }
        public string CampaignReference { get; set; } = null;
        public decimal? Discount { get; set; } = null;
        public decimal BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public TransactionStatus Status { get; set; }
    }



    public enum TransactionStatus
    {
        Pending,
        Confirmed,
        Failed,
        Expired
    }

    public enum BlockchainEventType
    {
        OrderRegistered,
        OrderExecuted,
        OrderExpired,
        TransactionConfirmed,
        TransactionFailed,
        BlockMined, 
        NetworkStatus
    }
}
