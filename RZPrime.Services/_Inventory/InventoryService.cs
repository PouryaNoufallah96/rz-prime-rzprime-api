//using MongoDB.Driver;
//using RZPrime.Domain.Repositories.Contracts;
//using static RZPrime.Utilities.Constants.RegisterMode;
//using MongoDB.Driver.Linq;
//using RZPrime.Domain.Collections;
//using RZPrime.Services._Inventory.DTOs.Storages;
//using RZPrime.Services._Price.DTOs.Settings;
//using System.Collections.Concurrent;
//using RZPrime.Services._Price;
//using RZPrime.Utilities.Exceptions.Common;
//namespace RZPrime.Services._Inventory
//{


//    /// <summary>
//    ///  Manages inventory operations and asset tracking.
//    ///  Provides functionality to:
//    ///     - Calculate and retrieve current asset quantities
//    ///     - Track pending and finalized orders
//    ///     - Monitor real-time asset prices
//    ///     - Maintain accurate inventory balances
//    /// </summary>
//    public class InventoryService(IInventoryRepository _inventoryRepository,
//        AvailableTokensSettings _availableTokenSettings,
//        InventoryStorage _inventoryStorage,
//        IPriceService _priceService,
//        IOrderRepository _orderRepository)
//        : IInventoryService, IScopedDependency
//    {


//        /// <summary>
//        /// Get current quantities of all assets in inventory.
//        /// Retrieves current quantities of all assets from the database and returns it in a serialized format.
//        /// </summary>
//        /// <returns></returns>
//        /// <exception cref="BadRequestException"></exception>
//        public async Task<ConcurrentDictionary<string, InventoryData>> GetExistingTokensDataAsync()
//        {
//            var inventories = await _inventoryRepository.AsQueryable().ToListAsync();
//            if (inventories == null || inventories.Count < 1)
//                throw new BadRequestException("there is no inventory!");

//            var orderSums = await GetSumOfPaidAndRegisteredOrderedTokensAsync();

//            var inventoryDict = inventories.ToDictionary(inv => inv.TokenName);

//            foreach (var kvp in orderSums)
//            {
//                if (inventoryDict.TryGetValue(kvp.Key, out var inv))
//                {
//                    inv.InitialQuantity -= kvp.Value;
//                    if (inv.InitialQuantity < 0)
//                        inv.InitialQuantity = 0;
//                }
//            }

//            var prices = await _priceService.FetchAllPricesForInternalUsageAsync();

//            var result = new ConcurrentDictionary<string, InventoryData>();
//            foreach (var inv in inventoryDict.Values)
//            {
//                if (prices.TryGetValue(inv.TokenName, out var priceData))
//                {
//                    result[inv.TokenName] = new InventoryData
//                    {
//                        LastUpdated = DateTime.UtcNow,
//                        Price = priceData,  
//                        Quantity = inv.InitialQuantity
//                    };
//                }
//                else
//                {
//                    result[inv.TokenName] = new InventoryData
//                    {
//                        LastUpdated = DateTime.UtcNow,
//                        Price = null, 
//                        Quantity = inv.InitialQuantity
//                    };
//                }
//            }

//            return result;
//        }



//        /// <summary>
//        /// this method is for order service
//        /// when there is change in order service we should call this method to sync the quantity of token
//        /// </summary>
//        /// <param name="tokenName"></param>
//        /// <returns></returns>
//        public async Task SyncInventoryQuantityAsync(string tokenName)
//        {
//            var inventory = await _inventoryRepository.AsQueryable().FirstOrDefaultAsync(q => q.TokenName.Equals(tokenName, StringComparison.CurrentCultureIgnoreCase));

//            if (inventory != null)
//            {
//                var sumOfOrder = await _orderRepository.AsQueryable()
//                   .Where(q => q.TokenName.Equals(tokenName, StringComparison.CurrentCultureIgnoreCase))
//                   .Where(q => q.State == OrderState.Registered || q.State == OrderState.Paid)
//                   .SumAsync(q => q.TokenAmount);

//                var quantity = inventory.InitialQuantity - sumOfOrder;
//                quantity = quantity < 0 ? 0 : quantity;
//                _inventoryStorage.UpdateQuantity(tokenName, quantity);
//            }
//        }



//        /// <summary>
//        /// this method use for initialize the storage for the quantity field
//        /// </summary>
//        /// <returns></returns>
//        public async Task InitializeAllQuantitiesAsync()
//        {
//            var allInventories = await _inventoryRepository.AsQueryable().ToListAsync();
//            if (allInventories == null || allInventories.Count < 1)
//            {
//                await InitializeRecordsForInventoryAsync();
//                allInventories = await _inventoryRepository.AsQueryable().ToListAsync();
//            }



//            var ordersByToken = await _orderRepository.AsQueryable()
//                .Where(q => q.State == OrderState.Registered || q.State == OrderState.Paid)
//                .GroupBy(q => q.TokenName)
//                .Select(g => new
//                {
//                    TokenName = g.Key,
//                    TotalQuantity = g.Sum(x => x.TokenAmount)
//                })
//                .ToListAsync();

//            var ordersDict = ordersByToken.ToDictionary(o => o.TokenName, o => o.TotalQuantity);

//            foreach (var inventory in allInventories)
//            {
//                ordersDict.TryGetValue(inventory.TokenName, out var totalOrderQty);

//                var quantity = inventory.InitialQuantity - totalOrderQty;
//                quantity = quantity < 0 ? 0 : quantity;

//                _inventoryStorage.UpdateQuantity(inventory.TokenName, quantity);
//            }
//        }



//        /// <summary>
//        /// this method use for initialize the inventories quantity data if not exists in db
//        /// </summary>
//        /// <returns></returns>
//        private async Task InitializeRecordsForInventoryAsync()
//        {
//            if (_availableTokenSettings != null && _availableTokenSettings.Count > 0)
//            {
//                var inventories = new List<Inventory> { };

//                foreach (var token in _availableTokenSettings)
//                {
//                    inventories.Add(new Inventory
//                    {
//                        TokenAddress = token.Address,
//                        TokenName = token.Name,
//                        InitialQuantity = 20000,
//                        TokenNetwork = token.Network,
//                    });
//                }

//                await _inventoryRepository.InsertManyAsync(inventories);
//            }
//        }



//        /// <summary>
//        /// Calculate total quantity of assets in paid and registered orders.
//        /// Retrieves all orders, splits them by state, and aggregates their quantities by asset.
//        /// </summary>
//        private async Task<Dictionary<string, decimal>> GetSumOfPaidAndRegisteredOrderedTokensAsync()
//        {
//            var orders = await _orderRepository.AsQueryable()
//                .Where(q => q.State == OrderState.Registered || q.State == OrderState.Paid)
//                .ToListAsync();

//            var result = orders
//            .GroupBy(o => o.TokenName)
//            .ToDictionary(
//                g => g.Key,
//                g => g.Sum(o => o.TokenAmount)
//            );

//            return result;
//        }



//        /// <summary>
//        /// use for get one inventory by token name for internal usages
//        /// </summary>
//        /// <param name="tokenName"></param>
//        /// <returns></returns>
//        /// <exception cref="NotFoundException"></exception>
//        public async Task<Inventory> GetOneByTokenNameForInternalUsageAsync(string tokenName)
//        {
//            var inventory = await _inventoryRepository.FindOneAsync(q => q.TokenName.Equals(tokenName, StringComparison.CurrentCultureIgnoreCase))
//               ?? throw new NotFoundException("inventory of token not found!");
//            return inventory;
//        }
//    }
//}
