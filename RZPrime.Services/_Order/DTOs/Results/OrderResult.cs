using RZPrime.Domain.Collections;

namespace RZPrime.Services._Order.DTOs.Results
{



    public class OrderListResult
    {
        public List<OrderResult> Data { get; set; } = [];
        public int PageCount { get; set; } = 0;
        public int TotalCount { get; set; } = 0;
    }


    public class OrderResult
    {
        public string OrderId { get; set; }
        public DateTime CreatedMoment { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedMoment { get; set; } = null;
        public string UserPublicKey { get; set; }
        public string WalletAddress { get; set; }
        public string UserStageId { get; set; }
        public UserStageType Stage { get; set; }

        public string TokenName { get; set; }
        public decimal Quantity { get; set; }
        public string TokenNetwork { get; set; }
        public string TokenAddress { get; set; }
        public decimal TokenEffectivePrice { get; set; }

        public decimal LoanAmount { get; set; }
        public decimal LoanInterestAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public decimal ProfitRatePerMonth { get; set; }
        public int MonthDuration { get; set; }
        public DateTime PayOffDate { get; set; }
        public string TokenAmountInWei { get; set; }
        public string PayAmountInWei { get; set; }

        public OrderState State { get; set; } = OrderState.Registered;
        public DateTime? ChangeStateMoment { get; set; } = null;
        public string Promotion { get; set; }
        public List<OrderTransactionMeta> TransactionsMetaData { get; set; } 



    }
}
