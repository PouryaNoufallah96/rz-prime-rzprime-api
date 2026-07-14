using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.MongoDatabase.Documents;

namespace RZPrime.Domain.Collections
{

    [MonjoCollectionName("Campaigns")]
    public class Campaign : BaseDocument
    {
        public string CampaignReference { get; set; }

        public CampaignState State { get; set; }
        public CampaignType Type { get; set; }
        public decimal DiscountPercentage { get; set; } //0 -100


        //for time based campaigns
        public DateTime? FromOrderRegisterTime { get; set; } = null;
        public DateTime? ToOrderRegisterTime { get; set; } = null;

        public DateTime ExpireMoment { get; set; } 

        //for ref based campaigns
        public List<string> Orders { get; set; } = null;
        public List<string> Wallets { get; set; } = null;


        public string RegisterHash { get; set; }
        public DateTime? RegisterMoment { get; set; } = null;

        public string CancelHash { get; set; }
        public DateTime? CancelMoment { get; set; } = null;

        //public string Code { get; set; } 

    }

    public enum CampaignType { TimeBased, RefBased } //RefBased means orders and wallets

    public enum CampaignState { NotRegistered, Registered, Canceled ,Expired }  
}
