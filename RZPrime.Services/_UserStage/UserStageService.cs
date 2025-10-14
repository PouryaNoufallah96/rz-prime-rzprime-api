using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories.Contracts;
using RZPrime.Services._UserStage.DTOs.Settings;
using RZPrime.Utilities.Exceptions.Common;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._UserStage
{
    public class UserStageService(
        IUserStageRepository _userStageRepository,
        UserStageSetting _userStageSetting,
        UserStageSetting userStageSetting) : IUserStageService, IScopedDependency
    {




        /// <summary>
        /// this is for get a user stages for internal usages
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="publicKey"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        public async Task<List<UserStage>> GetUserStagesByWalletAddressForInternalUsage(string walletAddress)
        {
            var result = await _userStageRepository.AsQueryable()
                .Where(q => q.WalletAddress == walletAddress).ToListAsync();

            if (result == null || result.Count < 1) throw new NotFoundException("User Stages not found!");
            return result;
        }


        public async Task<List<UserStage>> GetUserStagesByWalletAddressForInternalForPureWalletUsage(string walletAddress)
        {
            var result = await _userStageRepository.AsQueryable()
                .Where(q => q.WalletAddress == walletAddress).ToListAsync();

            return result;
        }


        /// <summary>
        /// this methods use for increase drop count for user
        /// </summary>
        /// <param name="stage"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
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


        /// <summary>
        /// this methods use for getting min and max of user and check excludes wallets
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="UserStageType"></param>
        /// <returns></returns>
        public (decimal, decimal) GetMinAndMaxBuyAmountWithStage(string walletAddress, UserStageType UserStageType)
        {
            var excludeWallets = new List<string> { "0xf3B97d7A9e0BCCa9912a575564d531cE2B6c0f6B", "0x798457be80878b1f132e3A516b4b44E197CE3076"
            ,"0xa756b5f89290cC1C57cB88ee010E8D97EfB118C0"};

            if (excludeWallets.Any(x => string.Equals(x, walletAddress, StringComparison.OrdinalIgnoreCase))
                 && UserStageType == UserStageType.Regular)
            {
                return (1, 1000);
            }

            var stageSetting = GetStageSetting(UserStageType);
            return (stageSetting.MinimumBuyAmount, stageSetting.MaximumBuyAmount);

        }



        /// <summary>
        /// this method use for sync drop counts in stages
        /// reset drop count after 6 month in the stage
        /// </summary>
        /// <returns></returns>
        public async Task SyncDropCountsInStagesAsync()
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddMonths(-6);

            foreach (UserStageType stageType in Enum.GetValues(typeof(UserStageType)))
            {

                //TODO : remove when other stage added
                if (stageType != UserStageType.Regular) continue;

                var stageSetting = GetStageSetting(stageType);

                var filter = Builders<UserStage>.Filter.And(
                     Builders<UserStage>.Filter.Eq(x => x.Stage, stageType),
                     Builders<UserStage>.Filter.Lt(x => x.AvailableDrop, stageSetting.AvailableDropCount),
                     Builders<UserStage>.Filter.Or(
                         Builders<UserStage>.Filter.And(
                             Builders<UserStage>.Filter.Ne(x => x.ModifiedMoment, null),
                             Builders<UserStage>.Filter.Lte(x => x.ModifiedMoment, cutoff)
                         ),
                         Builders<UserStage>.Filter.And(
                             Builders<UserStage>.Filter.Eq(x => x.ModifiedMoment, null),
                             Builders<UserStage>.Filter.Lte(x => x.CreatedMoment, cutoff)
                         )
                     )
                 );

                var update = Builders<UserStage>.Update
                    .Set(x => x.AvailableDrop, stageSetting.AvailableDropCount)
                    .Set(x => x.ModifiedMoment, now);

                var result = await _userStageRepository.UpdateManyAsync(filter, update);                
            }
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
