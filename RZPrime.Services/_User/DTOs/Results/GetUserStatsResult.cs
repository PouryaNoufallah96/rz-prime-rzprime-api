using RZPrime.Domain.Collections;

namespace RZPrime.Services._User.DTOs.Results
{
    public class GetUserStatsResult
    {
        public UserStageType Stage { get; set; }
        public decimal Mining { get; set; } = 0m;
        public decimal AvailableLoanAmount { get; set; }
        public decimal SumOfMining { get; set; }
        public int AvailableDropCount { get; set; }

    }


} 
