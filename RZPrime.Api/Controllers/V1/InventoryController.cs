using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RZPrime.Services._Inventory;
using RZPrime.Services._Inventory.DTOs.Storages;
using RZPrime.Utilities.Api;
using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.Filters;
using Swashbuckle.AspNetCore.Annotations;
using System.Collections.Concurrent;

namespace RZPrime.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class InventoryController(IBlockChainInventory _inventoryService) : ApiBaseController
    {

        [HttpGet("[action]")]
        [Authorize]
        [CustomRateLimit(maxAttemptsCount: 60)]
        [SwaggerOperation(Summary = "Get inventory for all tokens", Tags = ["Inventory"])]
        public async Task<ConcurrentDictionary<string, InventoryData>> GetExistingTokensDataAsync()
        {
            return await _inventoryService.GetExistingTokensDataAsync();
        }



    }
}
