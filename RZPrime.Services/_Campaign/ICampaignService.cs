using RZPrime.Domain.Collections;
using RZPrime.Services._Campaign.DTOs;
using RZPrime.Utilities.DTOs;

namespace RZPrime.Services._Campaign
{
    public interface ICampaignService
    {
        Task<bool> CreateCampaignAsync(CreateCampaignUpdate createCampaignUpdate);
        Task<bool> CancelCampaignAsync(CancelCampaignUpdate cancelCampaignUpdate);
        Task<CampaignListResult> GetAllCampaignsAsync(Pagination pagination);
        Task<List<Campaign>> GetAvailableCampaignsAsync();
        Task<Campaign?> GetBestCampaignAsync(string walletAddress, string orderId, DateTime orderRegisteredMoment);
        Task FindCampaignForExpireAsync();
    }
}
