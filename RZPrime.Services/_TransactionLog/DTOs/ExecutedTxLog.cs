using RZPrime.Domain.Collections;

namespace RZPrime.Services._TransactionLog.DTOs
{
    public class ExecutedTxLog
    {
        public string OrderId { get; set; }
        public TransactionLogHistory ExecuteData { get; set; }
    }


    public class ConfirmTxLog
    {
        public string OrderId { get; set; }
        public TransactionLogHistory ConfirmData { get; set; }
    }

    public class FailTxLog
    {
        public string OrderId { get; set; }
        public TransactionLogHistory FailData { get; set; }
    }


    public class RegisteredTxLog
    {
        public string OrderId { get; set; }
        public string UserWallet { get; set; }
        public string TokenName { get; set; }
        public decimal TokenAmount { get; set; }
        public decimal USDTAmount { get; set; }
        public TransactionLogHistory RegisteredData { get; set; }
    }

    //public class CreateOrderConfirmedTransactionLogUpdate
    //{
    //    public string OrderId { get; set; } 
    //    public string UserWallet { get; set; }
    //    public TransactionLogHistory  ConfirmData { get; set; }  
    //}
}
