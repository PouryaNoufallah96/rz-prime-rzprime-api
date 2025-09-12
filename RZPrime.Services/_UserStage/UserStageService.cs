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


        //TODO : auto calc for stage

    }
}
