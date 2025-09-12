using System.Transactions;

namespace RZPrime.Services._TransactionLog.DTOs.Results
{
    public class TransactionListResult
    {
        public List<TransactionResult> Transactions { get; set; }
        public int PageCount { get; set; } = 0;
        public int TotalCount { get; set; } = 0;
    }
     

    public class TransactionResult
    {
        public DateTime CreatedMoment { get; set; }
        public string TransactionLogId { get; set; }
        public string OrderId { get; set; }
        public string PublicKey { get; set; }
        public string Hash { get; set; }
        public string TokenAddress { get; set; }
        public DateTime TimeStamp { get; set; }
        public TransactionStatus Status { get; set; }
    }

}
