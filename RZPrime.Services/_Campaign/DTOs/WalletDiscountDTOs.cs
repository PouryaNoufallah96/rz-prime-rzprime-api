namespace RZPrime.Services._Campaign.DTOs
{
    public class SetWalletDiscountRequest
    {
        public string WalletAddress { get; set; }
        public decimal DiscountPercentage { get; set; }
    }

    public class CancelWalletDiscountRequest
    {
        public string WalletAddress { get; set; }
    }

    public class WalletDiscountResult
    {
        public string WalletAddress { get; set; }
        public decimal CurrentDiscount { get; set; }
        public string TransactionHash { get; set; }
        public DateTime AppliedAt { get; set; }
    }
}

