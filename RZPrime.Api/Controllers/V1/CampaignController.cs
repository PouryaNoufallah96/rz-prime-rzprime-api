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

    }
}
