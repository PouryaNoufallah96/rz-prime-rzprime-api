using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RZPrime.Services._BlockChain;
using RZPrime.Services._PancakeSwap;
using RZPrime.Services._PancakeSwap.DTOs;
using RZPrime.Utilities.Api;
using RZPrime.Utilities.Filters;

namespace RZPrime.Api.Controllers.V1
{

    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class TestController(/*IBlockChainService blockChainService, IPancakeSwapService _pancakeSwapService*/) : ApiBaseController
    {
        [HttpGet("[action]")]
        public string Test()
        => "Hello world"; 

        //[HttpGet("[action]")]
        //public async Task<Dictionary<string, decimal>> GetContractBalancesAsync()
        //{
        //    return await blockChainService.GetContractBalancesAsync();
        //}

        //[HttpGet("[action]")]
        //public async Task<Dictionary<string, decimal>> GetBalancesMulticallAsync()
        //{
        //    return await blockChainService.GetBalancesMultiCallAsync();
        //}


        //[HttpPost("[action]")]
        //public async Task<decimal> GetMultiCallOptimalSwapAmountInBSCAsync(GetSwapAmountUpdate update)
        //{
        //    return await _pancakeSwapService.GetMultiCallOptimalSwapAmountInBSCAsync(update);
        //}






    }
}
