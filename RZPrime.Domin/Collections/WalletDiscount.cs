using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.MongoDatabase.Documents;

namespace RZPrime.Domain.Collections
{

    [MonjoCollectionName("WalletDiscounts")]
    public class WalletDiscount : BaseDocument
    {
        public string WalletAddress { get; set; }
        public decimal CurrentDiscount { get; set; }
        public List<WalletDiscountHistory> History { get; set; } = []; 
    }

    public class WalletDiscountHistory
    {
        public DateTime RegisterMoment { get; set; } = DateTime.UtcNow;
        public string RegisterHash { get; set; }
        public decimal Discount { get; set; }

    }

}
