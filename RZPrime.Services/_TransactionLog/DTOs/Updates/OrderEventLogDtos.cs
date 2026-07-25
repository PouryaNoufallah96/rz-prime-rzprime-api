using RZPrime.Domain.Collections;
using System.Numerics;

namespace RZPrime.Services._TransactionLog.DTOs.Updates
{

    public class CommonLogData
    {
        public string OrderId { get; set; }
        public string Address { get; set; }
        public string UserWallet { get; set; }
        public string Hash { get; set; }
        public decimal BlockNumber { get; set; }
        public BlockchainEventType EventType { get; set; }
        public TransactionStatus Status { get; set; }
    }


    public class OrderRegisteredLogData : CommonLogData
    {
        public BigInteger TokenAmount { get; set; }
        public string CampaignId { get; set; }
        public BigInteger DiscountBps { get; set; }
    }


    public class OrderExpiredLogData : CommonLogData 
    {

    }

    public class OrderExecutedLogData : CommonLogData 
    {
        public BigInteger RzusdPaid { get; set; }
        public BigInteger UsdValue { get; set; }
    }


}
