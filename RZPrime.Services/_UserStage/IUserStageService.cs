using RZPrime.Domain.Collections;

namespace RZPrime.Services._UserStage
{
    public interface IUserStageService
    {
        Task<string> InitializeUserStageAsync(string walletAddress, string userPublicKey);
        Task<List<UserStage>> GetUserStagesByWalletAddressForInternalUsage(string walletAddress);
        Task IncreaseDropCountAsync(UserStage stage);
        public (decimal, decimal) GetMinAndMaxBuyAmountWithStage(string walletAddress, UserStageType UserStageType);
        Task<List<UserStage>> GetUserStagesByWalletAddressForInternalForPureWalletUsage(string walletAddress);
        Task SyncDropCountsInStagesAsync();
    }
}
 