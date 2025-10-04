using Microsoft.Extensions.DependencyInjection;
using RZPrime.Services._BlockChain;
using RZPrime.Utilities.Services;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Schedulers
{
    public class PollMissingLogsScheduler(IServiceProvider serviceProvider) : SchedulerBase(serviceProvider, TimeSpan.FromMinutes(10)), IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider)
        {
            var priceService = scopedProvider.GetRequiredService<IBlockChainService>();

            await priceService.PollMissingLogsAsync();
        }
    }


}
