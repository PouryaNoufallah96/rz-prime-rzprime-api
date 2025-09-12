using RZPrime.Domain.Collections;
using RZPrime.Services._Inventory.DTOs.Storages;
using System.Collections.Concurrent;

namespace RZPrime.Services._Inventory
{
    public interface IBlockChainInventory
    {
        Task<ConcurrentDictionary<string, InventoryData>> GetExistingTokensDataAsync();
        Task SyncInventoryQuantityAsync(string tokenName);
        Task InitializeAllQuantitiesAsync();
        Task<decimal> GetQuantityAsyncGetOneByTokenNameForInternalUsageAsync(string tokenName);
    }
}
