namespace RZPrime.Services._Inventory.DTOs.Results
{
    public class SumOfPaidAndRegisteredTokensResult
    {
        public Dictionary<string, decimal> PaidData { get; set; }
        public Dictionary<string, decimal> RegisteredData { get; set; }
    }
}
