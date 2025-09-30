using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories.Contracts;
using MongoDB.Driver.Linq;
using static RZPrime.Utilities.Constants.RegisterMode;
using MongoDB.Driver;
using RZPrime.Utilities.Exceptions.Common;
using RZPrime.Services._UserStage.DTOs.Settings;

namespace RZPrime.Services._UserStage
{
    public class UserStageService(
        IUserStageRepository _userStageRepository,
        UserStageSetting _userStageSetting,
        UserStageSetting userStageSetting,
        IOrderRepository _orderRepository) : IUserStageService, IScopedDependency
    {




        /// <summary>
        /// this is for get a user stages for internal usages
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="publicKey"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        public async Task<List<UserStage>> GetUserStagesByWalletAddressForInternalUsage(string walletAddress, string publicKey)
        {
            var result = await _userStageRepository.AsQueryable()
                .Where(q => q.WalletAddress == walletAddress && q.UserPublicKey == publicKey).ToListAsync();

            if (result == null || result.Count < 1) throw new NotFoundException("User Stages not found!");
            return result;
        }

        public async Task IncreaseDropCountAsync(UserStage stage)
        {
            if (stage.AvailableDrop <= 0)
                throw new BadRequestException("No available drops left for this stage.");


            stage.AvailableDrop -= 1;
            await _userStageRepository.ReplaceOneAsync(stage);
        }


        /// <summary>
        /// this method use for initialize stage for new users
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="userPublicKey"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        public async Task<string> InitializeUserStageAsync(string walletAddress, string userPublicKey)
        {
            var exists = await _userStageRepository.ExistsAsync(q => q.WalletAddress == walletAddress);
            if (exists) throw new BadRequestException("user stage already exists!");

            var newUserStage = new UserStage
            {
                WalletAddress = walletAddress,
                UserPublicKey = userPublicKey,
                Stage = UserStageType.Regular,
                AvailableDrop = userStageSetting.Regular.AvailableDropCount
            };

            await _userStageRepository.InsertOneAsync(newUserStage);
            return newUserStage.UserStageId;

        }


        public (decimal, decimal) GetMinAndMaxBuyAmountWithStage(string walletAddress, UserStageType UserStageType)
        {
            var excludeWallets = new List<string> { "0xf3B97d7A9e0BCCa9912a575564d531cE2B6c0f6B" };

            if (excludeWallets.Any(x => string.Equals(x, walletAddress, StringComparison.OrdinalIgnoreCase))
                 && UserStageType == UserStageType.Regular)
            {
                return (1, 1000);
            }

            var stageSetting = GetStageSetting(UserStageType);
            return (stageSetting.MinimumBuyAmount, stageSetting.MaximumBuyAmount);

        }


        /// <summary>
        /// for getting stage setting by stage type enum
        /// </summary>
        /// <param name="stage"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        private StageSetting GetStageSetting(UserStageType stage)
        {
            return stage switch
            {
                UserStageType.Regular => _userStageSetting.Regular,
                UserStageType.Gold => _userStageSetting.Gold,
                UserStageType.Premium => _userStageSetting.Premium,
                UserStageType.X => _userStageSetting.X,
                _ => throw new BadRequestException("wrong stage!"),
            };
        }

        //TODO : auto calc for stage

    }
}
