using RZPrime.Domain.Collections;
using RZPrime.Utilities.Attributes;

namespace RZPrime.Services._Campaign.DTOs
{


    public class CampaignBannerResult
    {
        /// <summary>
        /// درصد تخفیف برای ولت (از WalletDiscount یا Campaign)
        /// </summary>
        public decimal DiscountPercentage { get; set; }

        /// <summary>
        /// مقدار حداقل سفارش (USD)
        /// </summary>
        public decimal MinUSDValue { get; set; }

        /// <summary>
        /// مقدار حداکثر سفارش (USD)
        /// </summary>
        public decimal MaxUSDValue { get; set; }

        /// <summary>
        /// تعداد نفرات مجاز در کمپین (0 = نامحدود)
        /// </summary>
        public int MaxUsers { get; set; }

        /// <summary>
        /// آیا این تخفیف از ولت است یا کمپین
        /// </summary>
        public bool IsWalletDiscount { get; set; }

        /// <summary>
        /// شناسه کمپین (اگر از کمپین باشد)
        /// </summary>
        public string CampaignReference { get; set; }
    }

    public class CreateCampaignUpdate
    {
        public DateTime FromOrderRegisterTime { get; set; } 
        public DateTime ToOrderRegisterTime { get; set; } 
        public int MaxUsers { get; set; } = 0;
        public decimal MinUSDValue { get; set; }
        public decimal MaxUSDValue { get; set; }
        public decimal DiscountPercentage { get; set; }
        public bool FirstOrder { get; set; } = true;
    }
    public class EditCampaignUpdate : CreateCampaignUpdate
    {
        public string  CampaignReference { get; set; }
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
        public DateTime? ModifiedMoment { get; set; }
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
        public DateTime? RegisterMoment { get; set; }

        public string RemoveHash { get; set; }
        public DateTime? RemoveMoment { get; set; } 

        public List<CampaignEditHistory> EditHistory { get; set; } 
    }

}
