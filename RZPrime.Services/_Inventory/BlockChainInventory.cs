using RZPrime.Domain.Repositories.Contracts;
using RZPrime.Services._Inventory.DTOs.Storages;
using RZPrime.Services._Price.DTOs.Settings;
using RZPrime.Services._Price;
using static RZPrime.Utilities.Constants.RegisterMode;
using RZPrime.Services._BlockChain;
using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories;
using RZPrime.Utilities.Exceptions.Common;
using System.Collections.Concurrent;
using MongoDB.Driver.Linq;
using MongoDB.Driver;
namespace RZPrime.Services._Inventory
{
    public class BlockChainInventory(
        IBlockChainService _blockChainService,
        InventoryStorage _inventoryStorage,
        IPriceService _priceService,
        IOrderRepository _orderRepository

        ) : IBlockChainInventory, IScopedDependency
    {


        /// <summary>
        /// Get current quantities of all assets in inventory.
        /// Retrieves current quantities of all assets from the database and returns it in a serialized format.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        public async Task<ConcurrentDictionary<string, InventoryData>> GetExistingTokensDataAsync()
        {

            var inventories = await _blockChainService.GetBalancesMultiCallAsync();
            var orderSums = await GetSumOfPaidAndRegisteredOrderedTokensAsync();


            foreach (var kvp in orderSums)
            {
                if (inventories.TryGetValue(kvp.Key, out var inv))
                {
                    inv -= kvp.Value;
                    if (inv < 0)
                        inv = 0;
                }
            }

            var prices = await _priceService.FetchAllPricesForInternalUsageAsync();

            var result = new ConcurrentDictionary<string, InventoryData>();
            foreach (var inv in inventories)
            {
                if (prices.TryGetValue(inv.Key, out var priceData))
                {
                    result[inv.Key] = new InventoryData
                    {
                        LastUpdated = DateTime.UtcNow,
                        Price = priceData,
                        Quantity = inv.Value
                    };
                }
                else
                {
                    result[inv.Key] = new InventoryData
                    {
                        LastUpdated = DateTime.UtcNow,
                        Price = null,
                        Quantity = inv.Value
                    };
                }
            }

            return result;
        }

        public async Task<decimal> GetQuantityAsyncGetOneByTokenNameForInternalUsageAsync(string tokenName)
        {
            return await _blockChainService.GetContractSingleBalanceAsync(tokenName);
        }


        /// <summary>
        /// this method is for order service
        /// when there is change in order service we should call this method to sync the quantity of token
        /// </summary>
        /// <param name="tokenName"></param>
        /// <returns></returns>
        public async Task SyncInventoryQuantityAsync(string tokenName)
        {
            var balance = await _blockChainService.GetContractSingleBalanceAsync(tokenName);

            var sumOfOrder = await _orderRepository.AsQueryable()
               .Where(q => q.TokenName.Equals(tokenName, StringComparison.CurrentCultureIgnoreCase))
               .Where(q => q.State == OrderState.Registered)
               .SumAsync(q => q.TokenAmount);

            var quantity = balance - sumOfOrder;
            quantity = quantity < 0 ? 0 : quantity;
            _inventoryStorage.UpdateQuantity(tokenName, quantity);
        }

     

        /// <summary>
        /// this method use for initialize the storage for the quantity field
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAllQuantitiesAsync()
        {
            var inventories = await _blockChainService.GetBalancesMultiCallAsync();

            var ordersByToken = await _orderRepository.AsQueryable()
                .Where(q => q.State == OrderState.Registered)
                .GroupBy(q => q.TokenName)
                .Select(g => new
                {
                    TokenName = g.Key,
                    TotalQuantity = g.Sum(x => x.TokenAmount)
                })
                .ToListAsync();

            var ordersDict = ordersByToken.ToDictionary(o => o.TokenName, o => o.TotalQuantity);

            foreach (var inventory in inventories)
            {
                ordersDict.TryGetValue(inventory.Key, out var totalOrderQty);

                var quantity = inventory.Value - totalOrderQty;
                quantity = quantity < 0 ? 0 : quantity;

                _inventoryStorage.UpdateQuantity(inventory.Key, quantity);
            }
        }


        /// <summary>
        /// Calculate total quantity of assets in paid and registered orders.
        /// Retrieves all orders, splits them by state, and aggregates their quantities by asset.
        /// </summary>
        private async Task<Dictionary<string, decimal>> GetSumOfPaidAndRegisteredOrderedTokensAsync()
        {
            var orders = await _orderRepository.AsQueryable()
                .Where(q => q.State == OrderState.Registered)
                .ToListAsync();

            var result = orders
            .GroupBy(o => o.TokenName)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(o => o.TokenAmount)
            );

            return result;
        }

        
    }
}
