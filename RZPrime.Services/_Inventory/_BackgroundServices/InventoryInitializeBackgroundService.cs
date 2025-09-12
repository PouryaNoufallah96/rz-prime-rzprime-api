using Microsoft.Extensions.Hosting;
using RZPrime.Services._Price.DTOs.Settings;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._Inventory._BackgroundServices
{
    public class InventoryInitializeBackgroundService(IBlockChainInventory inventoryService) : BackgroundService, IHostedDependency
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await inventoryService.InitializeAllQuantitiesAsync();
        }
    }
}
