using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories.Contracts;
using RZPrime.Services._Campaign.DTOs;
using RZPrime.Utilities.DTOs;
using RZPrime.Utilities.Exceptions.Common;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._Campaign
{
    public class CampaignService(ICampaignRepository _campaignRepository, IMemoryCache _cache, IOrderRepository _orderRepository) : ICampaignService, IScopedDependency
    {
        private const string CampaignCacheKey = "ACTIVE_CAMPAIGNS";


        public async Task<bool> CreateCampaignAsync(CreateCampaignUpdate update)
        {
            if (update.ExpireMoment == null)
                throw new BadRequestException("Campaign expire moment is required.");

            if (update.ExpireMoment <= DateTime.UtcNow)
                throw new BadRequestException("Campaign expire moment must be in the future.");

            if (update.DiscountPercentage < 0 || update.DiscountPercentage > 100)
                throw new BadRequestException("Campaign discount percentage must be between 0 and 100.");

            if (update.Type == CampaignType.TimeBased)
            {
                if (update.FromOrderRegisterTime == null || update.ToOrderRegisterTime == null)
                    throw new BadRequestException("Campaign time is required.");

                if (update.FromOrderRegisterTime >= update.ToOrderRegisterTime)
                    throw new BadRequestException("Campaign time range is invalid.");

                if (update.ExpireMoment < update.ToOrderRegisterTime)
                    throw new BadRequestException("Campaign expire moment must be greater than or equal to ToOrderRegisterTime.");

                var hasDuplicate = await _campaignRepository
                    .AsQueryable()
                    .AnyAsync(x =>
                        x.State == CampaignState.Registered &&
                        x.Type == CampaignType.TimeBased &&
                        x.FromOrderRegisterTime <= update.ToOrderRegisterTime &&
                        x.ToOrderRegisterTime >= update.FromOrderRegisterTime);

                if (hasDuplicate)
                    throw new BadRequestException("Another time based campaign already exists in this time range.");
            }
            else if (update.Type == CampaignType.RefBased)
            {
                if ((update.Orders == null || !update.Orders.Any()) &&
                    (update.Wallets == null || !update.Wallets.Any()))
                    throw new BadRequestException("Campaign orders or wallets are required.");
            }
            else
            {
                throw new BadRequestException("Invalid campaign type.");
            }

            var campaign = new Campaign
            {
                CampaignReference = Guid.NewGuid().ToString("N"),

                CreatedMoment = DateTime.UtcNow,
                State = CampaignState.NotRegistered,
                Type = update.Type,
                ExpireMoment = update.ExpireMoment,
                DiscountPercentage = update.DiscountPercentage,

                FromOrderRegisterTime = update.Type == CampaignType.TimeBased ? update.FromOrderRegisterTime : null,
                ToOrderRegisterTime = update.Type == CampaignType.TimeBased ? update.ToOrderRegisterTime : null,

                Orders = update.Type == CampaignType.RefBased ? update.Orders?.Select(x => x.ToLowerInvariant()).ToList() ?? [] : null,
                Wallets = update.Type == CampaignType.RefBased ? update.Wallets?.Select(x => x.ToLowerInvariant()).ToList() ?? [] : null
            };

            //TODO : Send to blockchain for registration

            await _campaignRepository.InsertOneAsync(campaign);

            await RefreshCacheAsync();

            return true;
        }

        public async Task<bool> CancelCampaignAsync(CancelCampaignUpdate update)
        {
            var campaign = await _campaignRepository
                .AsQueryable()
                .FirstOrDefaultAsync(x =>
                    x.CampaignReference == update.CampaignReference);

            if (campaign == null)
                throw new Exception("Campaign not found.");

            if (campaign.State == CampaignState.Canceled)
                return true;

            campaign.State = CampaignState.Canceled;
            campaign.ModifiedMoment = DateTime.UtcNow;

            //TODO : Send to blockchain for cancellation
            //campaign.CancelHash = Guid.NewGuid().ToString("N");
            //campaign.CancelMoment = DateTime.UtcNow;
            await _campaignRepository.ReplaceOneAsync(campaign);
            await RemoveCampaignFromOrdersAsync(new List<string> { update.CampaignReference });
            await RefreshCacheAsync();

            return true;
        }

        public async Task<CampaignListResult> GetAllCampaignsAsync(Pagination pagination)
        {
            var skip = (pagination.Page - 1) * pagination.Size;

            var query = _campaignRepository.AsQueryable();

            var totalCount = await query.CountAsync();

            var campaigns = await query
                .OrderByDescending(x => x.CreatedMoment)
                .Skip(skip)
                .Take(pagination.Size)
                .ToListAsync();

            var result = campaigns.Select(x => new CampaignResult
            {
                CreatedMoment = x.CreatedMoment,
                ModifiedMoment = x.ModifiedMoment,
                CampaignReference = x.CampaignReference,
                State = x.State,
                Type = x.Type,
                DiscountPercentage = x.DiscountPercentage,
                FromOrderRegisterTime = x.FromOrderRegisterTime,
                ToOrderRegisterTime = x.ToOrderRegisterTime,
                Orders = x.Orders,
                Wallets = x.Wallets,
                ExpireMoment = x.ExpireMoment,
            }).ToList();

            return new CampaignListResult
            {
                Data = result,
                TotalCount = totalCount,
                PageCount = (int)Math.Ceiling((double)totalCount / pagination.Size)
            };
        }

        public async Task<List<Campaign>> GetAvailableCampaignsAsync()
        {
            if (_cache.TryGetValue(CampaignCacheKey, out List<Campaign>? campaigns))
                return campaigns!;

            campaigns = await _campaignRepository
                .AsQueryable()
                .Where(x => x.State == CampaignState.Registered && x.ExpireMoment > DateTime.UtcNow)
                .OrderByDescending(x => x.DiscountPercentage)
                .ToListAsync();

            _cache.Set(
                CampaignCacheKey,
                campaigns);

            return campaigns;
        }

        public async Task<Campaign?> GetBestCampaignAsync(string walletAddress, string orderId, DateTime orderRegisteredMoment)
        {
            var campaigns = await GetAvailableCampaignsAsync();
            walletAddress = walletAddress.ToLowerInvariant();
            orderId = orderId.ToLowerInvariant();

            return campaigns.FirstOrDefault(c =>
            {
                return c.Type switch
                {
                    CampaignType.TimeBased =>
                        c.FromOrderRegisterTime.HasValue &&
                        c.ToOrderRegisterTime.HasValue &&
                        orderRegisteredMoment >= c.FromOrderRegisterTime &&
                        orderRegisteredMoment <= c.ToOrderRegisterTime,

                    CampaignType.RefBased =>
                        c.Orders.Contains(orderId) &&
                        c.Wallets.Contains(walletAddress),

                    _ => false
                };
            });
        }

        private async Task RemoveCampaignFromOrdersAsync(List<string> campaignReferences)
        {
            if (campaignReferences == null || campaignReferences.Count == 0)
                return;

            var filter = Builders<Order>.Filter.And(
                Builders<Order>.Filter.Eq(x => x.State, OrderState.Registered),
                Builders<Order>.Filter.In(x => x.CampaignReference, campaignReferences)
            );

            var update = Builders<Order>.Update
                .Set(x => x.CampaignReference, null)
                .Set(x => x.CampaignDiscount, null);

            await _orderRepository.UpdateManyAsync(filter, update);
        }

        public async Task FindCampaignForExpireAsync()
        {
            var expiredCampaigns = await _campaignRepository.AsQueryable()
            .Where(x =>
            x.State == CampaignState.Registered &&
            x.ExpireMoment <= DateTime.UtcNow)
            .ToListAsync();

            if (expiredCampaigns.Count == 0)
                return;

            var campaignIds = expiredCampaigns
                .Select(x => x.Id)
                .ToList();

            var filter = Builders<Campaign>.Filter
                .In(x => x.Id, campaignIds);

            var update = Builders<Campaign>.Update
                .Set(x => x.State, CampaignState.Expired)
                .Set(x => x.ModifiedMoment, DateTime.UtcNow);

            await _campaignRepository.UpdateManyAsync(filter, update);
            await RemoveCampaignFromOrdersAsync(expiredCampaigns.Select(x => x.CampaignReference).ToList());
            await RefreshCacheAsync();
        }

        private async Task RefreshCacheAsync()
        {
            var campaigns = await _campaignRepository
                .AsQueryable()
                .Where(x =>
                    x.State == CampaignState.Registered &&
                    x.ExpireMoment > DateTime.UtcNow)
                .OrderByDescending(x => x.DiscountPercentage)
                .ToListAsync();

            _cache.Set(CampaignCacheKey, campaigns);
        }


        //public async Task<CampaignResult?> GetAvailableCampaignAsync(string walletAddress, string orderId, DateTime orderRegısteredMoment)
        //{
        //    var campaigns = await _campaignRepository
        //        .AsQueryable()
        //        .Where(x => x.State == CampaignState.Registered)
        //        .ToListAsync();

        //    Campaign? bestCampaign = null;

        //    foreach (var campaign in campaigns)
        //    {
        //        bool isValid = campaign.Type switch
        //        {
        //            CampaignType.TimeBased =>
        //                campaign.FromOrderRegisterTime.HasValue &&
        //                campaign.ToOrderRegisterTime.HasValue &&
        //                orderRegısteredMoment >= campaign.FromOrderRegisterTime.Value &&
        //                orderRegısteredMoment <= campaign.ToOrderRegisterTime.Value,

        //            CampaignType.RefBased =>
        //                campaign.Orders != null &&
        //                campaign.Wallets != null &&
        //                campaign.Orders.Any(x =>
        //                    x.Equals(orderId, StringComparison.OrdinalIgnoreCase)) &&
        //                campaign.Wallets.Any(x =>
        //                    x.Equals(walletAddress, StringComparison.OrdinalIgnoreCase)),

        //            _ => false
        //        };

        //        if (!isValid)
        //            continue;

        //        if (bestCampaign == null ||
        //            campaign.DiscountPercentage > bestCampaign.DiscountPercentage)
        //        {
        //            bestCampaign = campaign;
        //        }
        //    }

        //    if (bestCampaign == null)
        //        return null;

        //    return new CampaignResult
        //    {
        //        CreatedMoment = bestCampaign.CreatedMoment,
        //        ModifiedMoment = bestCampaign.ModifiedMoment,
        //        CampaignReference = bestCampaign.CampaignReference,

        //        State = bestCampaign.State,
        //        Type = bestCampaign.Type,
        //        DiscountPercentage = bestCampaign.DiscountPercentage,

        //        FromOrderRegisterTime = bestCampaign.FromOrderRegisterTime,
        //        ToOrderRegisterTime = bestCampaign.ToOrderRegisterTime,

        //        Orders = bestCampaign.Orders,
        //        Wallets = bestCampaign.Wallets
        //    };
        //}

    }
}
