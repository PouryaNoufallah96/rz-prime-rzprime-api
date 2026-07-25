using System.Numerics;

namespace RZPrime.Services._BlockChain.DTOs
{
    public class CreateCampaignOnBlockChainRequest
    {
        public string CampaignReference { get; set; }
        public DateTime FromOrderRegisterTime { get; set; }
        public DateTime ToOrderRegisterTime { get; set; }
        public int MaxUsers { get; set; }
        public decimal MinUSDValue { get; set; }
        public decimal MaxUSDValue { get; set; }
        public decimal DiscountPercentage { get; set; }
        public bool FirstOrder { get; set; } = true;
    }

    public class CampaignOnChainResult
    {
        public BigInteger StartAt { get; set; }
        public BigInteger EndAt { get; set; }
        public BigInteger MaxUsers { get; set; }
        public BigInteger Claimed { get; set; }
        public decimal MinUsdValue { get; set; }
        public decimal MaxUsdValue { get; set; }
        public decimal DiscountPercentage { get; set; }
        public bool FirstOrder { get; set; }
        public bool Exists { get; set; }
        public bool Removed { get; set; }
    }
}
