namespace Services._Price._RZPriceService.DTOs
{
    public class RZPriceResult
    {
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public string TokenName { get; set; }
        public string TokenAddress { get; set; }
        public string TokenNetwork { get; set; }
        public decimal Price { get; set; }
        public decimal ChangePrice24hPercentage { get; set; }
    }


}
