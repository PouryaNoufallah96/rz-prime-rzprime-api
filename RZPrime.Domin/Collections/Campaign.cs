using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.MongoDatabase.Documents;

namespace RZPrime.Domain.Collections
{

    [MonjoCollectionName("Campaigns")]
    public class Campaign : BaseDocument
    {
        public string CampaignReference { get; set; }
        public DateTime FromOrderRegisterTime { get; set; }  //start at 
        public DateTime ToOrderRegisterTime { get; set; } // end at
        public int MaxUsers { get; set; } = 0;
        public decimal MinUSDValue { get; set; }
        public decimal MaxUSDValue { get; set; }
        public decimal DiscountPercentage { get; set; } //0 -100
        public bool FirstOrder { get; set; } = false;

        public CampaignState State { get; set; }

        public string RegisterHash { get; set; }
        public DateTime? RegisterMoment { get; set; } = null;
         
        public string RemoveHash { get; set; }
        public DateTime? RemoveMoment { get; set; } = null;

        public List<CampaignEditHistory> EditHistory { get; set; } = [];
    }

    public class CampaignEditHistory 
    {
        public string EditHash { get; set; }
        public DateTime? EditMoment { get; set; } = null;
        public string Changes { get; set; }
    }


    public enum CampaignState { Registered, Canceled ,Expired }  
}
