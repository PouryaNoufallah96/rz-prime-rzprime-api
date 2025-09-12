//using RZPrime.Domain.Collections;
//using RZPrime.Services._Inventory.DTOs.Storages;
//using System.Collections.Concurrent;

//namespace RZPrime.Services._Inventory
//{
//    public interface IInventoryService
//    {

//        Task<ConcurrentDictionary<string, InventoryData>> GetExistingTokensDataAsync();
//        //Task<decimal> GetOneInventoryByTokenNameAsync(string tokenName);
//        Task SyncInventoryQuantityAsync(string tokenName);
//        Task InitializeAllQuantitiesAsync();
//        Task<Inventory> GetOneByTokenNameForInternalUsageAsync(string tokenName);
//    }
//}
