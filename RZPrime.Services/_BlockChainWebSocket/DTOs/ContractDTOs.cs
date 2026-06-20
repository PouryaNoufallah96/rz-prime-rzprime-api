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
