using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._UserStage.DTOs.Settings
{
    public class UserStageSetting
    {
        public StageSetting Regular { get; set; } = new StageSetting();
        public StageSetting Gold { get; set; } = new StageSetting();
        public StageSetting Premium { get; set; } = new StageSetting();
        public StageSetting X { get; set; } = new StageSetting();
    }

    public class StageSetting
    {
        public decimal MinimumBuyAmount { get; set; }
        public decimal MaximumBuyAmount { get; set; } 
        public int MaximumPayOffMonth { get; set; }
        public decimal ProfitPercentage { get; set; }
        public int AvailableDropCount { get; set; }
    }

     
}
  