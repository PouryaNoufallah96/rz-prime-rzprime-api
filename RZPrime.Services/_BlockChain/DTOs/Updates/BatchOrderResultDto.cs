namespace RZPrime.Services._BlockChain.DTOs
{
    public class BatchOrderResultDto
    {
        public string UserAddress { get; set; }
        public string OrderId { get; set; }
        public bool Success { get; set; }
        public OrderDetails? OrderDetails { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
