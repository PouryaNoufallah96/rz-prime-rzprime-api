using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace RZPrime.Services._BlockChainWebSocket.DTOs
{

    // Event DTOs
    [Event("OrderRegistered")]
    public class OrderRegisteredEventDTO : IEventDTO
    {
        [Parameter("address", "user", 1, false)]
        public string User { get; set; }

        [Parameter("string", "orderId", 2, false)]
        public string OrderId { get; set; }

        [Parameter("uint256", "tokenAmount", 3, false)]
        public BigInteger TokenAmount { get; set; }

        [Parameter("bytes32", "campaignId", 4, false)]
        public byte[] CampaignId { get; set; }

        [Parameter("uint256", "discountBps", 5, false)]
        public BigInteger DiscountBps { get; set; }
    }

    [Event("OrderDropped")]
    public class OrderDroppedEventDTO : IEventDTO
    {
        [Parameter("address", "user", 1, false)]
        public string User { get; set; }

        [Parameter("string", "orderId", 2, false)]
        public string OrderId { get; set; }
    }

    [Event("OrderExpired")]
    public class OrderExpiredEventDTO : IEventDTO
    {
        [Parameter("address", "user", 1, false)]
        public string User { get; set; }

        [Parameter("string", "orderId", 2, false)]
        public string OrderId { get; set; }
    }

    [Event("OrderExecuted")]
    public class OrderExecutedEventDTO : IEventDTO
    {
        [Parameter("address", "user", 1, false)]
        public string User { get; set; }

        [Parameter("string", "orderId", 2, false)]
        public string OrderId { get; set; }

        [Parameter("uint256", "rzusdPaid", 3, false)]
        public BigInteger RzusdPaid { get; set; }

        [Parameter("uint256", "usdValue", 4, false)]
        public BigInteger UsdValue { get; set; }
    }

 
    [Event("Transfer")]
    public class TransferEventDTO : IEventDTO
    {
        [Parameter("address", "_from", 1, true)]
        public string From { get; set; }

        [Parameter("address", "_to", 2, true)]
        public string To { get; set; }

        [Parameter("uint256", "_value", 3, false)]
        public BigInteger Value { get; set; }
    }
}


//[Event("CampaignCreated")]
//public class CampaignCreatedEventDTO : IEventDTO
//{
//    [Parameter("bytes32", "campaignId", 1, false)]
//    public byte[] CampaignId { get; set; }

//    [Parameter("uint256", "startAt", 2, false)]
//    public BigInteger StartAt { get; set; }

//    [Parameter("uint256", "endAt", 3, false)]
//    public BigInteger EndAt { get; set; }

//    [Parameter("uint256", "maxUsers", 4, false)]
//    public BigInteger MaxUsers { get; set; }

//    [Parameter("uint256", "minUsdValue", 5, false)]
//    public BigInteger MinUsdValue { get; set; }

//    [Parameter("uint256", "maxUsdValue", 6, false)]
//    public BigInteger MaxUsdValue { get; set; }

//    [Parameter("uint256", "discountBps", 7, false)]
//    public BigInteger DiscountBps { get; set; }

//    [Parameter("bool", "firstOrder", 8, false)]
//    public bool FirstOrder { get; set; }
//}

//[Event("CampaignEdited")]
//public class CampaignEditedEventDTO : IEventDTO
//{
//    [Parameter("bytes32", "campaignId", 1, false)]
//    public byte[] CampaignId { get; set; }

//    [Parameter("uint256", "startAt", 2, false)]
//    public BigInteger StartAt { get; set; }

//    [Parameter("uint256", "endAt", 3, false)]
//    public BigInteger EndAt { get; set; }

//    [Parameter("uint256", "maxUsers", 4, false)]
//    public BigInteger MaxUsers { get; set; }

//    [Parameter("uint256", "minUsdValue", 5, false)]
//    public BigInteger MinUsdValue { get; set; }

//    [Parameter("uint256", "maxUsdValue", 6, false)]
//    public BigInteger MaxUsdValue { get; set; }

//    [Parameter("uint256", "discountBps", 7, false)]
//    public BigInteger DiscountBps { get; set; }

//    [Parameter("bool", "firstOrder", 8, false)]
//    public bool FirstOrder { get; set; }
//}

//[Event("CampaignRemoved")]
//public class CampaignRemovedEventDTO : IEventDTO
//{
//    [Parameter("bytes32", "campaignId", 1, false)]
//    public byte[] CampaignId { get; set; }
//}

//[Event("PremiumDiscountSet")]
//public class PremiumDiscountSetEventDTO : IEventDTO
//{
//    [Parameter("address", "wallet", 1, false)]
//    public string Wallet { get; set; }

//    [Parameter("uint256", "bps", 2, false)]
//    public BigInteger Bps { get; set; }
//}

