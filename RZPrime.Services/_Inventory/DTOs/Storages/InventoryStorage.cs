using Microsoft.AspNetCore.SignalR;
using RZPrime.Services._Inventory._Hub;
using RZPrime.Services._Price.DTOs.Results;
using System.Collections.Concurrent;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._Inventory.DTOs.Storages
{
    /// <summary>
    /// key is token address 
    /// </summary>
    public class InventoryStorage : ConcurrentDictionary<string, InventoryData>, ISelfSingletonDependency
    {
        private readonly IHubContext<InventoryHub> _hubContext;

        public InventoryStorage(IHubContext<InventoryHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void UpdatePrice(string tokenName, PriceResult price)
        {
            AddOrUpdate(
                tokenName,
                new InventoryData { Price = price, Quantity = 0, LastUpdated = DateTime.UtcNow },
                (key, existing) =>
                {
                    existing.Price = price;
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                }
            );

            _hubContext.Clients.All.SendAsync("NotifyInventory", this);
        }

        public void UpdateQuantity(string tokenName, decimal quantity)
        {
            AddOrUpdate(
                tokenName,
                new InventoryData { Price = null, Quantity = quantity, LastUpdated = DateTime.UtcNow },
                (key, existing) =>
                {
                    existing.Quantity = quantity;
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                }
            );
            _hubContext.Clients.All.SendAsync("NotifyInventory", this);
        }
    }


    public class InventoryData
    {
        public decimal Quantity { get; set; }
        public PriceResult Price { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
 