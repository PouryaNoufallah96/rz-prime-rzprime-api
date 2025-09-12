using Microsoft.Extensions.DependencyInjection;
using RZPrime.Services._Log;
using RZPrime.Utilities.Services;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Schedulers
{
    public class LogCleanerScheduler(IServiceProvider serviceProvider)
        : SchedulerBase(serviceProvider, TimeSpan.FromDays(14)), IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider)
        {
            var logService = scopedProvider.GetRequiredService<ILogService>();

            await logService.HardDeleteLogsLogsAsync();
            await logService.HardDeleteRequestLogsAsync();
        }
    }
}
 