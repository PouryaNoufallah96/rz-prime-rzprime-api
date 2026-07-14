using Microsoft.Extensions.DependencyInjection;
using RZPrime.Services._Campaign;
using RZPrime.Utilities.Services;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Schedulers
{
    public class CampaignExpireScheduler(IServiceProvider serviceProvider)
        : SchedulerBase(serviceProvider, TimeSpan.FromHours(1)), IHostedDependency
    {
        protected override async Task HandleAsync(IServiceProvider scopedProvider)
        {
            var campaignService = scopedProvider.GetRequiredService<ICampaignService>();

            await campaignService.FindCampaignForExpireAsync();
        }
    }
}
