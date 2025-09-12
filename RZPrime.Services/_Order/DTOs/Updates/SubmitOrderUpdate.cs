using RZPrime.Domain.Collections;

namespace RZPrime.Services._Order.DTOs.Updates
{
    public class SubmitOrderUpdate 
    {
        public string TokenName { get; set; }
        public decimal USDTAmount { get; set; }    

        public UserStageType SelectedStage { get; set; }
        public int MonthDuration { get; set; } 
    }
}
