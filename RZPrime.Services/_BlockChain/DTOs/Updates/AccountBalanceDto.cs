using System.Numerics;

namespace RZPrime.Services._BlockChain.DTOs
{
    public class AccountBalanceDto
    {
        public bool Success { get; set; }
        public string Address { get; set; }
        public BigInteger BalanceInWei { get; set; }
        public decimal BalanceInEther { get; set; }
        public string FormattedBalance => $"{BalanceInEther:N6} ETH/BNB"; // Adjust currency symbol as needed
        public string? ErrorMessage { get; set; }
    }
}
