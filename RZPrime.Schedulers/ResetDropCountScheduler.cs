using Microsoft.Extensions.DependencyInjection;
using RZPrime.Services._UserStage;
using RZPrime.Utilities.Services;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Schedulers
{
    public class ResetDropCountScheduler(IServiceProvider serviceProvider) : SchedulerBase(serviceProvider, TimeSpan.FromDays(1)), IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider)
        {
            var userStageService = scopedProvider.GetRequiredService<IUserStageService>();
            await userStageService.SyncDropCountsInStagesAsync();
        }
    }


}
