using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories.Contracts;
using RZPrime.Services._BlockChain;
using RZPrime.Services._BlockChain.DTOs;
using RZPrime.Services._Campaign.DTOs;
using RZPrime.Utilities.DTOs;
using RZPrime.Utilities.Exceptions.Common;
using System.Security.Cryptography;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._Campaign
{
    public class CampaignService(ICampaignRepository _campaignRepository,
        IMemoryCache _cache,
        IWalletDiscountRepository _walletDiscountRepository,
        IBlockChainService _blockChainService,
        IOrderRepository _orderRepository) : ICampaignService, IScopedDependency
    {


        




        #region Banner & Discount Query

        public async Task<CampaignBannerResult> GetCampaignBannerAsync(string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
                throw new BadRequestException("Wallet address is required.");

            var normalizedWallet = walletAddress.ToLower();

           
            var walletDiscount = await _walletDiscountRepository
                .FindOneAsync(x => x.WalletAddress == normalizedWallet);

            if (walletDiscount != null && walletDiscount.CurrentDiscount > 0)
            {
                return new CampaignBannerResult
                {
                    DiscountPercentage = walletDiscount.CurrentDiscount,
                    MinUSDValue = 0,
                    MaxUSDValue = decimal.MaxValue,
                    MaxUsers = 0,
                    IsWalletDiscount = true,
                    CampaignReference = null
                };
            }
 
            var activeCampaign = await _campaignRepository
                .AsQueryable()
                .Where(x =>
                    x.State == CampaignState.Registered &&
                    x.FromOrderRegisterTime <= DateTime.UtcNow &&
                    x.ToOrderRegisterTime >= DateTime.UtcNow)
                .OrderByDescending(x => x.DiscountPercentage)
                .FirstOrDefaultAsync();

            if (activeCampaign == null)
                return null;

            return new CampaignBannerResult
            {
                DiscountPercentage = activeCampaign.DiscountPercentage,
                MinUSDValue = activeCampaign.MinUSDValue,
                MaxUSDValue = activeCampaign.MaxUSDValue,
                MaxUsers = activeCampaign.MaxUsers,
                IsWalletDiscount = false,
                CampaignReference = activeCampaign.CampaignReference
            };
        }

        #endregion

        #region Wallet Discount Management

        public async Task<WalletDiscountResult> SetWalletDiscountAsync(SetWalletDiscountRequest request)
        {
            if (request.DiscountPercentage < 0 || request.DiscountPercentage > 100)
                throw new BadRequestException("Discount percentage must be between 0 and 100.");

            var walletDiscount = await _walletDiscountRepository 
               .FindOneAsync(x => x.WalletAddress.ToLower() == request.WalletAddress.ToLower());

            if(walletDiscount == null)
            {
                walletDiscount = new WalletDiscount
                {
                    WalletAddress = request.WalletAddress.ToLower(),
                    CurrentDiscount = 0,
                    History = new List<WalletDiscountHistory>()
                };
                await _walletDiscountRepository.InsertOneAsync(walletDiscount);
            }

            if(walletDiscount.CurrentDiscount == request.DiscountPercentage)
               throw new BadRequestException("The wallet already has the specified discount percentage.");


            var blockchainResult = await _blockChainService.SetDiscountForAsync(request.WalletAddress, request.DiscountPercentage);

            if (blockchainResult == null || !blockchainResult.Success || string.IsNullOrEmpty(blockchainResult.TransactionHash))
                throw new BaseException("Failed to set wallet discount on blockchain.");

            walletDiscount.CurrentDiscount = request.DiscountPercentage;
            
            walletDiscount.History.Add(new WalletDiscountHistory
            {
                RegisterMoment = DateTime.UtcNow,
                RegisterHash = blockchainResult.TransactionHash,
                Discount = request.DiscountPercentage
            });

            await _walletDiscountRepository.ReplaceOneAsync(walletDiscount);

            return new WalletDiscountResult
            {
                WalletAddress = walletDiscount.WalletAddress,
                CurrentDiscount = walletDiscount.CurrentDiscount,
                TransactionHash = blockchainResult.TransactionHash,
                AppliedAt = DateTime.UtcNow
            };
        }

        public async Task<WalletDiscountResult> CancelWalletDiscountAsync(CancelWalletDiscountRequest request)
        {
            return await SetWalletDiscountAsync(new SetWalletDiscountRequest
            {
                WalletAddress = request.WalletAddress,
                DiscountPercentage = 0
            });
        }

        #endregion



        #region Campaign Management
        public async Task<bool> CreateCampaignAsync(CreateCampaignUpdate update)
        {
            ValidateCreateCampaignInputs(update);

            var hasDuplicate = await _campaignRepository
                .AsQueryable()
                .AnyAsync(x =>
                    x.State == CampaignState.Registered &&
                    x.FromOrderRegisterTime <= update.ToOrderRegisterTime &&
                    x.ToOrderRegisterTime >= update.FromOrderRegisterTime);

            if (hasDuplicate)
                throw new BadRequestException("Another time based campaign already exists in this time range.");

            var campaign = new Campaign
            {
                CampaignReference = GenerateBytes32HexId(),
                CreatedMoment = DateTime.UtcNow,
                DiscountPercentage = update.DiscountPercentage,
                FromOrderRegisterTime = update.FromOrderRegisterTime,
                ToOrderRegisterTime = update.ToOrderRegisterTime,
                MaxUsers = update.MaxUsers,
                MinUSDValue = update.MinUSDValue,
                MaxUSDValue = update.MaxUSDValue,
                FirstOrder = update.FirstOrder,
                State = CampaignState.Registered,
                RemoveHash = null,
                RegisterHash = null,
                RegisterMoment = null,
                RemoveMoment = null,
            };

            var blockchainResult = await _blockChainService.CreateCampaignAsync(new CreateCampaignOnBlockChainRequest
            {
                CampaignReference = campaign.CampaignReference,
                FromOrderRegisterTime = campaign.FromOrderRegisterTime,
                ToOrderRegisterTime = campaign.ToOrderRegisterTime,
                MaxUsers = campaign.MaxUsers,
                MinUSDValue = campaign.MinUSDValue,
                MaxUSDValue = campaign.MaxUSDValue,
                DiscountPercentage = campaign.DiscountPercentage,
                FirstOrder = campaign.FirstOrder
            });

            if (blockchainResult == null || !blockchainResult.Success || string.IsNullOrEmpty(blockchainResult.TransactionHash))
                throw new BaseException("Failed to create campaign on blockchain.");

            var registerHash = blockchainResult.TransactionHash;
            campaign.RegisterHash = registerHash;
            campaign.RegisterMoment = DateTime.UtcNow;

            await _campaignRepository.InsertOneAsync(campaign);

            return true;
        }
       
        public async Task<bool> EditCampaignAsync(EditCampaignUpdate update)
        {
            ValidateCreateCampaignInputs(update);

            var existing = await _campaignRepository
                .FindOneAsync(x => x.CampaignReference == update.CampaignReference && x.State == CampaignState.Registered);

            if (existing == null)
                throw new BadRequestException("Campaign not found or is not in a registered state.");

            var changesList = new List<string>();

            if (existing.FromOrderRegisterTime != update.FromOrderRegisterTime)
                changesList.Add($"FromOrderRegisterTime: {existing.FromOrderRegisterTime:u} → {update.FromOrderRegisterTime:u}");

            if (existing.ToOrderRegisterTime != update.ToOrderRegisterTime)
                changesList.Add($"ToOrderRegisterTime: {existing.ToOrderRegisterTime:u} → {update.ToOrderRegisterTime:u}");

            if (existing.MaxUsers != update.MaxUsers)
                changesList.Add($"MaxUsers: {existing.MaxUsers} → {update.MaxUsers}");

            if (existing.MinUSDValue != update.MinUSDValue)
                changesList.Add($"MinUSDValue: {existing.MinUSDValue} → {update.MinUSDValue}");

            if (existing.MaxUSDValue != update.MaxUSDValue)
                changesList.Add($"MaxUSDValue: {existing.MaxUSDValue} → {update.MaxUSDValue}");

            if (existing.DiscountPercentage != update.DiscountPercentage)
                changesList.Add($"DiscountPercentage: {existing.DiscountPercentage} → {update.DiscountPercentage}");

            if (existing.FirstOrder != update.FirstOrder)
                changesList.Add($"FirstOrder: {existing.FirstOrder} → {update.FirstOrder}");

            if (changesList.Count == 0)
                return true;

            var hasDuplicate = await _campaignRepository
                .AsQueryable()
                .AnyAsync(x =>
                    x.CampaignReference != update.CampaignReference &&
                    x.State == CampaignState.Registered &&
                    x.FromOrderRegisterTime <= update.ToOrderRegisterTime &&
                    x.ToOrderRegisterTime >= update.FromOrderRegisterTime);

            if (hasDuplicate)
                throw new BadRequestException("Another time based campaign already exists in this time range.");

            var blockchainResult = await _blockChainService.EditCampaignAsync(new CreateCampaignOnBlockChainRequest
            {
                CampaignReference = existing.CampaignReference,
                FromOrderRegisterTime = update.FromOrderRegisterTime,
                ToOrderRegisterTime = update.ToOrderRegisterTime,
                MaxUsers = update.MaxUsers,
                MinUSDValue = update.MinUSDValue,
                MaxUSDValue = update.MaxUSDValue,
                DiscountPercentage = update.DiscountPercentage,
                FirstOrder = update.FirstOrder
            });

            if (blockchainResult == null || !blockchainResult.Success || string.IsNullOrEmpty(blockchainResult.TransactionHash))
                throw new BaseException("Failed to edit campaign on blockchain.");

            existing.FromOrderRegisterTime = update.FromOrderRegisterTime;
            existing.ToOrderRegisterTime = update.ToOrderRegisterTime;
            existing.MaxUsers = update.MaxUsers;
            existing.MinUSDValue = update.MinUSDValue;
            existing.MaxUSDValue = update.MaxUSDValue;
            existing.DiscountPercentage = update.DiscountPercentage;
            existing.FirstOrder = update.FirstOrder;
            existing.RegisterHash = blockchainResult.TransactionHash;
            existing.RegisterMoment = DateTime.UtcNow;

            existing.EditHistory.Add(new CampaignEditHistory
            {
                EditHash = blockchainResult.TransactionHash,
                EditMoment = DateTime.UtcNow,
                Changes = string.Join(" | ", changesList)
            });

            await _campaignRepository.ReplaceOneAsync(existing);

            return true;
        }
        
        public async Task<bool> CancelCampaignAsync(CancelCampaignUpdate update)
        {
            var campaign = await _campaignRepository
                .AsQueryable()
                .FirstOrDefaultAsync(x =>
                    x.CampaignReference == update.CampaignReference);

            if (campaign == null)
                throw new BaseException("Campaign not found.");

            if (campaign.State != CampaignState.Registered)
                return true;

            campaign.State = CampaignState.Canceled;
            campaign.ModifiedMoment = DateTime.UtcNow;

            var blockchainResult = await _blockChainService.RemoveCampaignAsync(update.CampaignReference);

            if (blockchainResult == null || !blockchainResult.Success || string.IsNullOrEmpty(blockchainResult.TransactionHash))
                throw new BaseException("Failed to cancel campaign on blockchain.");

            campaign.RemoveHash = blockchainResult.TransactionHash;
            campaign.RemoveMoment = DateTime.UtcNow;

            await _campaignRepository.ReplaceOneAsync(campaign);

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
                DiscountPercentage = x.DiscountPercentage,
                FromOrderRegisterTime = x.FromOrderRegisterTime,
                ToOrderRegisterTime = x.ToOrderRegisterTime,
                RemoveHash = x.RemoveHash,
                EditHistory = x.EditHistory,
                FirstOrder = x.FirstOrder,
                MaxUsers = x.MaxUsers,
                MinUSDValue = x.MinUSDValue,
                MaxUSDValue = x.MaxUSDValue,
                RegisterHash = x.RegisterHash,
                RegisterMoment = x.RegisterMoment,
                RemoveMoment = x.RemoveMoment,

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
            
            var campaigns = await _campaignRepository
                .AsQueryable()
                .Where(x => x.State == CampaignState.Registered && x.FromOrderRegisterTime <= DateTime.UtcNow && x.ToOrderRegisterTime >= DateTime.UtcNow)
                .OrderByDescending(x => x.DiscountPercentage)
                .ToListAsync();

            return campaigns;
        }
  
        public async Task FindCampaignForExpireAsync()
        {
            var expiredCampaigns = await _campaignRepository.AsQueryable()
            .Where(x =>
            x.State == CampaignState.Registered &&
            x.ToOrderRegisterTime <= DateTime.UtcNow)
            .ToListAsync();

            if (expiredCampaigns == null || !expiredCampaigns.Any())
                return;

            //await CancelCampaignAsync(new CancelCampaignUpdate
            //{
            //    CampaignReference = expiredCampaign.CampaignReference
            //});

            foreach (var expiredCampaign in expiredCampaigns)
            {
                expiredCampaign.State = CampaignState.Expired;
                await _campaignRepository.ReplaceOneAsync(expiredCampaign);
            }
        }
         
        private static void ValidateCreateCampaignInputs(CreateCampaignUpdate update)
        {
            if (update.FromOrderRegisterTime == default || update.ToOrderRegisterTime == default)
                throw new BadRequestException("Campaign start and end moments are required.");

            if (update.ToOrderRegisterTime <= DateTime.UtcNow || update.FromOrderRegisterTime <= DateTime.UtcNow)
                throw new BadRequestException("Campaign start and end moments must be in the future.");

            if (update.ToOrderRegisterTime <= update.FromOrderRegisterTime)
                throw new BadRequestException("Campaign end moment must be after the start moment.");

            if (update.MaxUSDValue <= 0)
                throw new BadRequestException("Campaign max USD value must be greater than 0.");

            if (update.MinUSDValue <= 0)
                throw new BadRequestException("Campaign min USD value must be greater than 0.");

            if (update.MinUSDValue > update.MaxUSDValue)
                throw new BadRequestException("Campaign min USD value must be less than or equal to max USD value.");

            if (update.MaxUsers <= 0)
                throw new BadRequestException("Campaign max users must be greater than 0.");

            if (update.DiscountPercentage < 0 || update.DiscountPercentage > 100)
                throw new BadRequestException("Campaign discount percentage must be between 0 and 100.");
        }
        #endregion

      

        private string GenerateBytes32HexId()
        {
            var buffer = new byte[32];
            RandomNumberGenerator.Fill(buffer);

            var newId = BitConverter.ToString(buffer)
                .Replace("-", "")
                .ToLowerInvariant();

            return "0x" + newId;
        }


    }
}
