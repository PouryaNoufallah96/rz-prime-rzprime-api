using MongoDB.Bson.Serialization.Attributes;
using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.MongoDatabase.Documents;

namespace RZPrime.Domain.Collections
{

    [MonjoCollectionName("Orders")]
    public class Order : BaseDocument
    {
        public string OrderId { get; set; } = Guid.NewGuid().ToString("N");
        public string UserPublicKey { get; set; }
        public string WalletAddress { get; set; }
        public string UserStageId { get; set; }
        public UserStageType Stage { get; set; }

        public string TokenName { get; set; }
        public decimal TokenAmount { get; set; }
        public string TokenNetwork { get; set; }
        public string TokenAddress { get; set; }
        public decimal TokenPrice { get; set; }
        public decimal TokenEffectivePrice { get; set; }
        public decimal PriceImpactPercentage { get; set; }
        public string TokenAmountInWei { get; set; }
        public string PayAmountInWei { get; set; }

        public decimal LoanAmount { get; set; }
        public decimal LoanInterestAmount { get; set; } 
        public decimal FinalAmount { get; set; }
        public decimal ProfitRatePerMonth { get; set; }
        public int MonthDuration { get; set; }
        public DateTime PayOffDate { get; set; }
        
        public OrderState State { get; set; } = OrderState.Registered;
        public DateTime? ChangeStateMoment { get; set; } = null;
        public string Promotion { get; set; } 
        public List<string> Exceptions { get; set; }
        public string RegisterHash { get; set; }
        public List<OrderTransactionMeta> TransactionsMetaData { get; set; } = [];

        [BsonDefaultValue(null)] public string DropSignature { get; set; } = null;
        [BsonDefaultValue(null)] public string DropTransactionHash { get; set; } = null; 
    }
     

    public class OrderTransactionMeta
    {
        public DateTime CreateMoment { get; set; }
        public string Hash { get; set; }
        public TransactionStatus Status { get; set; }  
    }

    public enum OrderState { Registered, Drop, Paid }


}
