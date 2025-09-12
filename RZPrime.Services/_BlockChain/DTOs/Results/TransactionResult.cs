using System.Numerics;

namespace RZPrime.Services._BlockChain.DTOs.Results
{
    public class TransactionResult
    {
        public bool Success { get; set; }
        public string? TransactionHash { get; set; }
        public BigInteger? BlockNumber { get; set; }
        public BigInteger? GasUsed { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
