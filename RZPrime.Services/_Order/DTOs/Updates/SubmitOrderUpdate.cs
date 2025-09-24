using RZPrime.Domain.Collections;
using RZPrime.Utilities.Attributes;

namespace RZPrime.Services._Order.DTOs.Updates
{
    public class SubmitOrderUpdate 
    {
       [StringInputValidation(maxLength:50)] public string TokenName { get; set; }
        [NumericInputValidation] public decimal USDTAmount { get; set; }    

        public UserStageType SelectedStage { get; set; }
        [NumericInputValidation(max:3)]public int MonthDuration { get; set; } 
    }
}
