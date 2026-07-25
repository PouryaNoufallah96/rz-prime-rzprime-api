using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RZPrime.Services._Campaign;
using RZPrime.Services._Campaign.DTOs;
using RZPrime.Utilities.Api;
using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.DTOs;
using RZPrime.Utilities.Filters;
using RZPrime.Utilities.Permissions;
using Swashbuckle.AspNetCore.Annotations;

namespace RZPrime.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class CampaignController(ICampaignService _campaignService) : ApiBaseController
    {

        [HttpGet("[action]")]
        [Authorize(Permissions.Campaign)]
        [CustomRateLimit(maxAttemptsCount: 60)]
        [SwaggerOperation(Summary = "Get all campaigns", Tags = ["Campaign"])]
        public async Task<CampaignListResult> GetAllCampaignsAsync(Pagination pagination)
        {
            return await _campaignService.GetAllCampaignsAsync(pagination);
        }

        [HttpPost("[action]")]
        [Authorize(Permissions.Campaign)]
        [CustomRateLimit(maxAttemptsCount: 60)]
        [SwaggerOperation(Summary = "Create a new campaign", Tags = ["Campaign"])]
        public async Task<bool> CreateCampaignAsync(CreateCampaignUpdate createCampaignUpdate)
        {
            return await _campaignService.CreateCampaignAsync(createCampaignUpdate);
        }

        [HttpPost("[action]")]
        [Authorize(Permissions.Campaign)]
        [CustomRateLimit(maxAttemptsCount: 60)]
        [SwaggerOperation(Summary = "Cancel an existing campaign", Tags = ["Campaign"])]
        public async Task<bool> CancelCampaignAsync(CancelCampaignUpdate cancelCampaignUpdate)
        {
            return await _campaignService.CancelCampaignAsync(cancelCampaignUpdate);
        }

        [HttpPost("[action]")]
        [Authorize(Permissions.Campaign)]
        [CustomRateLimit(maxAttemptsCount: 60)]
        [SwaggerOperation(Summary = "Edit an existing campaign", Tags = ["Campaign"])]
        public async Task<bool> EditCampaignAsync(EditCampaignUpdate editCampaignUpdate)
        {
            return await _campaignService.EditCampaignAsync(editCampaignUpdate);
        }



        [HttpPost("[action]")]
        [Authorize(Permissions.Campaign)]
        [CustomRateLimit(maxAttemptsCount: 60)]
        [SwaggerOperation(Summary = "Set a wallet discount", Tags = ["Campaign"])]
        public async Task<WalletDiscountResult> SetWalletDiscountAsync(SetWalletDiscountRequest request)
        {
            return await _campaignService.SetWalletDiscountAsync(request);
        }

        [HttpPost("[action]")]
        [Authorize(Permissions.Campaign)]
        [CustomRateLimit(maxAttemptsCount: 60)]
        [SwaggerOperation(Summary = "Cancel a wallet discount", Tags = ["Campaign"])]
        public async Task<WalletDiscountResult> CancelWalletDiscountAsync(CancelWalletDiscountRequest request)
        {
            return await _campaignService.CancelWalletDiscountAsync(request);
        }
        
        
        [HttpGet("[action]")]
        [Authorize]
        [CustomRateLimit(maxAttemptsCount: 60)]
        [SwaggerOperation(Summary = "Get campaign banner for a wallet", Tags = ["User-Campaign"])]
        public async Task<CampaignBannerResult> GetCampaignBannerDataAsync() 
        {
            return await _campaignService.GetCampaignBannerAsync(WalletAddress);
        }



    }
}
