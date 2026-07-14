using RZPrime.Domain.Collections;
using RZPrime.Utilities.Attributes;

namespace RZPrime.Services._Campaign.DTOs
{
    public class CreateCampaignUpdate
    {
        public CampaignType Type { get; set; }
        public decimal DiscountPercentage { get; set; }
        public DateTime ExpireMoment { get; set; }

        public DateTime? FromOrderRegisterTime { get; set; } = null;
        public DateTime? ToOrderRegisterTime { get; set; } = null;

        public List<string> Orders { get; set; } = null;
        public List<string> Wallets { get; set; } = null;
    }

    public class CancelCampaignUpdate
    {
        [StringInputValidation] public string CampaignReference { get; set; }
    }

    public class CampaignListResult
    {
        public List<CampaignResult> Data { get; set; } = [];
        public int PageCount { get; set; } = 0;
        public int TotalCount { get; set; } = 0;
    }


    public class CampaignResult
    {
        public DateTime CreatedMoment { get; set; }
        public DateTime? ModifiedMoment { get; set; } = null;
        public string CampaignReference { get; set; }

        public CampaignState State { get; set; }
        public CampaignType Type { get; set; }
        public decimal DiscountPercentage { get; set; } 

        //for time based campaigns
        public DateTime? FromOrderRegisterTime { get; set; } 
        public DateTime? ToOrderRegisterTime { get; set; }

        public DateTime ExpireMoment { get; set; }


        //for ref based campaigns
        public List<string> Orders { get; set; } 
        public List<string> Wallets { get; set; } 
    }

}
