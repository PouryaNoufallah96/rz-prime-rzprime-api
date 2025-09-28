using Microsoft.Extensions.DependencyInjection;
using RZPrime.Services._Order;
using RZPrime.Utilities.Services;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Schedulers
{
    public class DropSignScheduler(IServiceProvider serviceProvider)
        : SchedulerBase(serviceProvider, TimeSpan.FromDays(3)), IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider)
        {
            var orderService = scopedProvider.GetRequiredService<IOrderService>();

            await orderService.SignDropsAsync();
        }
    }
}
