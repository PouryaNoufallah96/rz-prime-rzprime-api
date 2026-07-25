using RZPrime.Services._Order.DTOs.Results;
using RZPrime.Services._Order.DTOs.Updates;

namespace RZPrime.Services._Order
{
    public interface IOrderService
    {
        Task<SubmitOrderResponseResult> SubmitOrderAsync(SubmitOrderUpdate update, string userPublicKey, string walletAddress);
        Task<OrderListResult> GetAllUserOrdersAsync(GetAllUserOrdersUpdate update, string userPublicKey, string walletAddress);
        Task<OrderResult> DropOrderAsync(DropOrderUpdate update, string userPublicKey, string walletAddress);
        Task FindOrderToMakeDropAsync();
        Task SignDropsAsync();

        //Task SyncOrderRegisteredData(string orderId,string campaignReference,decimal discount); 


    }
}
 