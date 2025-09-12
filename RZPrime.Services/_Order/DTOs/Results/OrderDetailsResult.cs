using System.Numerics;

namespace RZPrime.Services._Order.DTOs.Results
{
    public class SubmitOrderResponseResult
    {
        public bool Success { get; set; }
        public decimal LoanAmount { get; set; }
        public string OrderId { get; set; }
        public string BlockchainTxHash { get; set; }
        public BigInteger? BlockNumber { get; set; }
        public BigInteger? GasUsed { get; set; }
        public OrderDetailsResult OrderDetails { get; set; }
    }

    public class OrderDetailsResult 
    {
        public string AssetName { get; set; }
        public decimal AssetQuantity { get; set; }
        public string PayOffDate { get; set; }
        public string WalletAddress { get; set; }
        public BigInteger TokenAmountWei { get; set; }
        public BigInteger PayAmountInWei { get; set; } 
        public long EndTimestamp { get; set; }
        public decimal FinalAmount { get; set; }
        public string RegisteredAmount { get; set; }
    }
}
