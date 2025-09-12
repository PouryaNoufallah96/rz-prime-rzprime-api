using RZPrime.Services._Inventory.DTOs.Storages;
using Microsoft.AspNetCore.SignalR;

namespace RZPrime.Services._Inventory._Hub
{
    public class InventoryHub : Hub
    {
        private readonly InventoryStorage _inventoryStorage;

        public InventoryHub(InventoryStorage inventoryStorage)
        {
            _inventoryStorage = inventoryStorage;
        }

        public override async Task OnConnectedAsync()
        {

            await Clients.Caller.SendAsync("NotifyInventory", _inventoryStorage);
            await base.OnConnectedAsync();
        }

        public async Task InventoryNotify()
        {
            await Clients.All.SendAsync("NotifyInventory", _inventoryStorage);
        }

    }

}
