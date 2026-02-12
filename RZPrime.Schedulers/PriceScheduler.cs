using Microsoft.Extensions.DependencyInjection;
using RZPrime.Services._Price;
using RZPrime.Utilities.Services;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Schedulers
{
    public class PriceScheduler(IServiceProvider serviceProvider) : SchedulerBase(serviceProvider, TimeSpan.FromMinutes(6)), IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider)
        {
            var priceService = scopedProvider.GetRequiredService<IPriceService>();
            await priceService.FetchAllPricesAsync();
        }
    }


}
