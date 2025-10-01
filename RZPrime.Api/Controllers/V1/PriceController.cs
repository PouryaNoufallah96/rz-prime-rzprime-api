using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RZPrime.Services._PancakeSwap;
using RZPrime.Services._PancakeSwap.DTOs;
using RZPrime.Services._Price;
using RZPrime.Utilities.Api;
using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.Filters;
using Swashbuckle.AspNetCore.Annotations;

namespace RZPrime.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class PriceController( IPancakeSwapService _pancakeSwapService) : ApiBaseController
    {
        


        [HttpPost("[action]")]
        [CustomRateLimit(maxAttemptsCount: 60)]
        [Authorize(RequireActiveUser = false)]
        [SwaggerOperation(Summary = "for swap amount with usdt amount and token name", Tags = ["Price"])]
        public async Task<decimal> GetSwapAmountAsync(GetSwapAmountUpdate update) 
        {
            return await _pancakeSwapService.GetMultiCallOptimalSwapAmountInBSCAsync(update);
        }

      


    }
}
  