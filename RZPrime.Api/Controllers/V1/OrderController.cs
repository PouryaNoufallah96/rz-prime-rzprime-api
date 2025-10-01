using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RZPrime.Services._Order;
using RZPrime.Services._Order.DTOs.Results;
using RZPrime.Services._Order.DTOs.Updates;
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
    public class OrderController(IOrderService _orderService) : ApiBaseController
    {

        [HttpPost("[action]")]
        [Authorize]
        [CustomRateLimit(maxAttemptsCount: 60)]
        [SwaggerOperation(Summary = "Get all orders for user", Tags = ["Order"])]
        public async Task<OrderListResult> GetAllUserOrders([FromBody] GetAllUserOrdersUpdate update)
        {
            return await _orderService.GetAllUserOrdersAsync(update, PublicKey, WalletAddress);
        }


        [HttpPost("[action]")]
        [Authorize]
        [CustomRateLimit(maxAttemptsCount:30)]
        [SwaggerOperation(Summary = "Submit order", Tags = ["Order"])]
        public async Task<SubmitOrderResponseResult> SubmitOrderAsync(SubmitOrderUpdate update)
        {
            return await _orderService.SubmitOrderAsync(update, PublicKey, WalletAddress);
        }


        [HttpPost("[action]")]
        [CustomRateLimit(maxAttemptsCount: 30)]
        [Authorize]
        [SwaggerOperation(Summary = "Drop order", Tags = ["Order"])]
        public async Task<OrderResult> DropOrderAsync(DropOrderUpdate update)
        {
            return await _orderService.DropOrderAsync(update, PublicKey, WalletAddress);
        }


        [HttpPost("[action]")]
        [Authorize]
        [ActiveUserOnly]
        [CustomRateLimit(maxAttemptsCount: 5)]
        [SwaggerOperation(Summary = "sync single order with blockchain, just for non paid orders", Tags = ["Order"])]
        public async Task<bool> SyncSingleOrderAsync(OrderIdUpdate update)
        {
            return await _orderService.SyncSingleOrderAsync(update,PublicKey,WalletAddress);
        }


    }
}
