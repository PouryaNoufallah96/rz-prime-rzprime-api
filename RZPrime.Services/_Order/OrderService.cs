using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories.Contracts;
using RZPrime.Services._BlockChain;
using RZPrime.Services._Campaign;
using RZPrime.Services._Inventory;
using RZPrime.Services._Order.DTOs.Results;
using RZPrime.Services._Order.DTOs.Updates;
using RZPrime.Services._PancakeSwap;
using RZPrime.Services._Price;
using RZPrime.Services._Price.DTOs.Results;
using RZPrime.Services._Price.DTOs.Settings;
using RZPrime.Services._UserStage;
using RZPrime.Services._UserStage.DTOs.Settings;
using RZPrime.Utilities.DTOs;
using RZPrime.Utilities.Exceptions.Common;
using RZPrime.Utilities.Extension;
using RZPrime.Utilities.Utilities;
using System.Numerics;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._Order
{
    public class OrderService(
        IOrderRepository _orderRepository,
        AvailableTokensSettings _availableTokenData,
        IPriceService _priceService,
        IBlockChainService _blockChainService,
        IBlockChainInventory _inventoryService,
        UserStageSetting _userStageSetting,
        IPancakeSwapService _pancakeSwapService,
        IHubContext<PaidOrderHub> _hubContext,
        ILogger<OrderService> _logger,
        ICampaignService _campaignService,
        IUserStageService _userStageService)
        : IOrderService, IScopedDependency
    {

        /// <summary>
        /// this method use sumbit order in db and blockchain
        /// </summary>
        /// <param name="update"></param>
        /// <param name="userPublicKey"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        /// <exception cref="BaseException"></exception>
        public async Task<SubmitOrderResponseResult> SubmitOrderAsync(SubmitOrderUpdate update, string userPublicKey, string walletAddress)
        {
            if (update.SelectedStage != UserStageType.Regular) throw new BadRequestException("Selected stage is not available!");
            if (update.MonthDuration > 3) throw new BadRequestException("Maximum of Month duration is Three month");

            var tokenData = ValidateToken(update.TokenName);
            var stageSetting = GetStageSetting(update.SelectedStage);
            var userStageId = await ValidateStageAsync(walletAddress, userPublicKey, stageSetting, update);
            var loanAmount = update.USDTAmount;

            var (tokenQauntity, effectivePriceResult) = await CalculateTokenAmountAsync(walletAddress, userPublicKey, stageSetting, update);
            var loanAmountInterest = CalculateLoanAmountInterest(stageSetting, update.MonthDuration, loanAmount);
            var finalAmount = loanAmount + loanAmountInterest;

            var newOrder = new Order
            {
                UserPublicKey = userPublicKey,
                WalletAddress = walletAddress,
                UserStageId = userStageId,
                Stage = update.SelectedStage,
                TokenName = update.TokenName.ToUpper(),
                TokenAmount = tokenQauntity.ToFixed(tokenData.PriceDecimalPlaces),
                TokenNetwork = "BSC",
                TokenAddress = tokenData.Address,
                TokenPrice = effectivePriceResult.Price,
                TokenEffectivePrice = effectivePriceResult.EffectivePrice.ToFixed(tokenData.PriceDecimalPlaces),
                PriceImpactPercentage = effectivePriceResult.Impact,
                LoanAmount = loanAmount.ToFixed(18),
                LoanInterestAmount = loanAmountInterest.ToFixed(18),
                FinalAmount = finalAmount.ToFixed(18),
                ProfitRatePerMonth = stageSetting.ProfitPercentage,
                MonthDuration = update.MonthDuration,
                PayOffDate = DateTime.UtcNow.AddMonths(update.MonthDuration),
                State = OrderState.Registered,
                ChangeStateMoment = null,
                Promotion = null,
                TokenAmountInWei = _blockChainService.ConvertToWei(tokenQauntity, tokenData.PriceDecimalPlaces).ToString(),
                PayAmountInWei = _blockChainService.ConvertToWei(finalAmount).ToString(),
            };

            try
            {
                var trasactionResult = await _blockChainService.RegisterOrderOnBlockChainAsync(newOrder);
                newOrder.RegisterHash = trasactionResult.TransactionHash;

                if (trasactionResult == null || trasactionResult.Success == false)
                {
                    newOrder.Exceptions = [trasactionResult.ErrorMessage];
                    newOrder.IsDeleted = true;
                    await _orderRepository.InsertOneAsync(newOrder);
                    throw new BaseException("error in blockchain!");
                }

                await _orderRepository.InsertOneAsync(newOrder);
                await _inventoryService.SyncInventoryQuantityAsync(newOrder.TokenName);

                await _hubContext.Clients.Group(newOrder.WalletAddress).SendAsync("NotifyPaidOrder", $"Successfully Register {newOrder.TokenName} Order.");

                return new SubmitOrderResponseResult
                {
                    Success = true,
                    LoanAmount = newOrder.LoanAmount,
                    OrderId = newOrder.Id,
                    BlockchainTxHash = trasactionResult.TransactionHash,
                    BlockNumber = trasactionResult.BlockNumber,
                    GasUsed = trasactionResult.GasUsed,
                    OrderDetails = new OrderDetailsResult
                    {
                        AssetName = newOrder.TokenName,
                        AssetQuantity = newOrder.TokenAmount,
                        PayOffDate = newOrder.PayOffDate.ToString("o"),
                        WalletAddress = newOrder.WalletAddress,
                        TokenAmountWei = BigInteger.Parse(newOrder.TokenAmountInWei),
                        EndTimestamp = new DateTimeOffset(newOrder.PayOffDate).ToUnixTimeSeconds(),
                        PayAmountInWei = BigInteger.Parse(newOrder.PayAmountInWei),
                        FinalAmount = newOrder.FinalAmount,
                    }
                };
            }
            catch (Exception ex)
            {
                newOrder.Exceptions = [ex.Message];
                newOrder.IsDeleted = true;
                await _orderRepository.ReplaceOneAsync(newOrder);
                throw new BaseException("error in submit order!");
            }

        }


        /// <summary>
        /// this method use for drop a single order 
        /// after drop sync inventory
        /// </summary>
        /// <param name="update"></param>
        /// <param name="userPublicKey"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        public async Task<OrderResult> DropOrderAsync(DropOrderUpdate update, string userPublicKey, string walletAddress)
        {
            var order = await _orderRepository.FindOneAsync(q => q.OrderId == update.OrderId
            && q.UserPublicKey == userPublicKey
            && q.WalletAddress.ToLower() == walletAddress.ToLower()
            && q.State == OrderState.Registered)
                ?? throw new NotFoundException("Order not found!");

            var userStages = await _userStageService.GetUserStagesByWalletAddressForInternalUsage(walletAddress);
            var selectedStage = userStages.FirstOrDefault(q => q.Stage == order.Stage) ?? throw new NotFoundException("user stage not found!");
            if (selectedStage.AvailableDrop <= 0) throw new BadRequestException("No available drops left for this stage.");

            order.DropSignature = update.Signature;
            order.DropTransactionHash = null;
            order.State = OrderState.Drop;
            order.ChangeStateMoment = DateTime.UtcNow;
            await _orderRepository.ReplaceOneAsync(order);

            await _inventoryService.SyncInventoryQuantityAsync(order.TokenName);
            await _userStageService.IncreaseDropCountAsync(selectedStage);

            await _hubContext.Clients.Group(order.WalletAddress).SendAsync("NotifyPaidOrder", "Successfully Drop Order.");

            return new OrderResult
            {
                OrderId = order.OrderId,
                CreatedMoment = order.CreatedMoment,
                ModifiedMoment = order.ModifiedMoment,
                UserPublicKey = order.UserPublicKey,
                WalletAddress = order.WalletAddress,
                UserStageId = order.UserStageId,
                Stage = order.Stage,

                TokenName = order.TokenName,
                Quantity = order.TokenAmount,
                TokenNetwork = order.TokenNetwork,
                TokenAddress = order.TokenAddress,
                TokenEffectivePrice = order.TokenEffectivePrice,

                LoanAmount = order.LoanAmount,
                LoanInterestAmount = order.LoanInterestAmount,
                FinalAmount = order.FinalAmount,
                ProfitRatePerMonth = order.ProfitRatePerMonth,
                MonthDuration = order.MonthDuration,
                PayOffDate = order.PayOffDate,

                State = order.State,
                ChangeStateMoment = order.ChangeStateMoment,
                Promotion = order.Promotion
            };
        }


        /// <summary>
        /// this method use for get all of orders for user
        /// </summary>
        /// <param name="pagination"></param>
        /// <param name="userPublicKey"></param>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        public async Task<OrderListResult> GetAllUserOrdersAsync(GetAllUserOrdersUpdate update, string userPublicKey, string walletAddress)
        {

            var pagination = update.Pagination;

            var skip = (pagination.Page - 1) * pagination.Size;

            var query = _orderRepository.AsQueryable();

            if (update.States != null && update.States.Count > 0)
            {
                query = query.Where(q => update.States.Contains(q.State));
            }

            var totalCount = await query
                .CountAsync(x => x.WalletAddress.ToLower() == walletAddress.ToLower());

            var orders = await query
             .Where(x => x.WalletAddress.ToLower() == walletAddress.ToLower())
             .OrderByDescending(x => x.CreatedMoment)
             .Skip(skip)
             .Take(pagination.Size)
             .ToListAsync();


            var result = orders.Select(order => new OrderResult
            {
                CreatedMoment = order.CreatedMoment,
                ModifiedMoment = order.ModifiedMoment,
                OrderId = order.OrderId,
                State = order.State,
                WalletAddress = walletAddress,
                ChangeStateMoment = order.ChangeStateMoment,
                FinalAmount = order.FinalAmount,
                LoanAmount = order.LoanAmount,
                LoanInterestAmount = order.LoanInterestAmount,
                MonthDuration = order.MonthDuration,
                PayOffDate = order.PayOffDate,
                ProfitRatePerMonth = order.ProfitRatePerMonth,
                Promotion = order.Promotion,
                Quantity = order.TokenAmount,
                Stage = order.Stage,
                TokenAddress = order.TokenAddress,
                TokenEffectivePrice = order.TokenEffectivePrice,
                TokenName = order.TokenName,
                TokenNetwork = order.TokenNetwork,
                UserStageId = order.UserStageId,
                PayAmountInWei = null,               
                TokenAmountInWei = order.TokenAmountInWei,
                TransactionsMetaData = order.TransactionsMetaData,
                CampaignDiscount = order.CampaignDiscount,
            }).ToList();

            var pageCount = (int)Math.Ceiling((double)totalCount / pagination.Size);

            return new OrderListResult
            {
                Data = result,
                TotalCount = totalCount,
                PageCount = pageCount
            };
        }


        public async Task<OrderResult> GetOrderDetailAsync(GetOrderDetailUpdate update, string walletAddress)
        {
            var order = await _orderRepository.AsQueryable()
            .Where(x => x.WalletAddress.ToLower() == walletAddress.ToLower())
            .Where(x => x.OrderId == update.OrderId).FirstOrDefaultAsync();

            if(order == null)
            {
                throw new BadRequestException("order not found!");
            }

            var paymentAmountInWei = await _blockChainService.PreviewPaymentAmountAsync(walletAddress, update.OrderId);

            var result = new OrderResult
            {
                CreatedMoment = order.CreatedMoment,
                ModifiedMoment = order.ModifiedMoment,
                OrderId = order.OrderId,
                State = order.State,
                WalletAddress = walletAddress,
                ChangeStateMoment = order.ChangeStateMoment,
                FinalAmount = order.FinalAmount,
                LoanAmount = order.LoanAmount,
                LoanInterestAmount = order.LoanInterestAmount,
                MonthDuration = order.MonthDuration,
                PayOffDate = order.PayOffDate,
                ProfitRatePerMonth = order.ProfitRatePerMonth,
                Promotion = order.Promotion,
                Quantity = order.TokenAmount,
                Stage = order.Stage,
                TokenAddress = order.TokenAddress,
                TokenEffectivePrice = order.TokenEffectivePrice,
                TokenName = order.TokenName,
                TokenNetwork = order.TokenNetwork,
                UserStageId = order.UserStageId,
                PayAmountInWei = paymentAmountInWei.ToString(),
                TokenAmountInWei = order.TokenAmountInWei,
                TransactionsMetaData = order.TransactionsMetaData,
                CampaignDiscount = order.CampaignDiscount
            };

            return result;
        }


        /// <summary>
        /// thid method find expire method and make it drop
        /// </summary>
        /// <returns></returns>
        public async Task FindOrderToMakeDropAsync()
        {
            var now = DateTime.UtcNow;
            var orders = await _orderRepository.AsQueryable()
                .Where(q => q.State == OrderState.Registered)
                .Where(q => q.PayOffDate >= now)
                .OrderBy(q => q.CreatedMoment)
                .ToListAsync();

            if (orders != null && orders.Count > 0)
            {
                var filter = Builders<Order>.Filter.And(
                Builders<Order>.Filter.Eq(x => x.State, OrderState.Registered),
                Builders<Order>.Filter.Lte(x => x.PayOffDate, now));

                var update = Builders<Order>.Update
                  .Set(x => x.State, OrderState.Drop)
                  .Set(x => x.ChangeStateMoment, now)
                  .Set(x => x.ModifiedMoment, now);

                var result = await _orderRepository.UpdateManyAsync(filter, update);

                if (result != null && result.ModifiedCount > 0)
                {
                    _logger.LogInformation("{Count} orders expired and moved to Drop state.", result.ModifiedCount);
                }

            }

        }


        public async Task SignDropsAsync()
        {
            var dropsForSign = await _orderRepository.AsQueryable()
                .Where(q => q.State == OrderState.Drop
                         && q.DropSignature != null
                         && q.DropTransactionHash == null
                         && q.CreatedMoment > new DateTime(2025, 9, 29))
                .ToListAsync();

            if (dropsForSign.Count > 0)
            {
                var chunks = dropsForSign.Chunk(10);

                foreach (var chunk in chunks)
                {
                    var txHash = await _blockChainService.SignDropOnBlockChainAsync(chunk.ToList());

                    if (!string.IsNullOrEmpty(txHash))
                    {
                        var orderIds = chunk.Select(q => q.OrderId).ToList();

                        var filter = Builders<Order>.Filter.In(o => o.OrderId, orderIds);
                        var update = Builders<Order>.Update.Set(o => o.DropTransactionHash, txHash);

                        var result = await _orderRepository.UpdateManyAsync(filter, update);
                        if (result != null && result.ModifiedCount > 0)
                        {
                            _logger.LogInformation("Updated {ModifiedCount} orders with DropTransactionHash {TxHash}.",
                                result.ModifiedCount, txHash);
                        }
                    }
                }
            }
        }



        //public async Task SyncOrderRegisteredData(string orderId, string campaignReference, decimal discount)
        //{
        //    if (discount <= 0)
        //    {
        //        _logger.LogInformation("Order {OrderId} has no discount. Campaign sync skipped.", orderId);
        //        return;
        //    }

        //    try
        //    {
        //        var updateDefinition = Builders<Order>.Update
        //            .Set(x => x.CampaignReference, campaignReference)
        //            .Set(x => x.CampaignDiscount, discount);

        //        var result = await _orderRepository.FindOneAndUpdateAsync(
        //            filter: x => x.OrderId == orderId,
        //            update: updateDefinition);

        //        _logger.LogInformation(
        //            "Order {OrderId} synced successfully. Campaign: {CampaignReference}, Discount: {Discount}%",
        //            orderId, campaignReference, discount);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex,
        //            "Failed to sync order campaign data. OrderId: {OrderId}, Campaign: {CampaignReference}, Discount: {Discount}",
        //            orderId, campaignReference, discount);
        //        throw;
        //    }
        //}




        #region PRIVATE METHODS


        /// <summary>
        /// check for existing token by name and return token setting
        /// </summary>
        /// <param name="tokenName"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        private AvailableTokenData ValidateToken(string tokenName)
        {
            var tokenData = _availableTokenData.FirstOrDefault(q => q.Name.Equals(tokenName, StringComparison.CurrentCultureIgnoreCase))
                ?? throw new BadRequestException($"Unsupported token name! {tokenName}");
            return tokenData;
        }


        /// <summary>
        /// this mehod validate the stage existing, payoff month, amount of loan and remaining with frozen
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="publicKey"></param>
        /// <param name="stage"></param>
        /// <param name="loanAmount"></param>
        /// <param name="monthCount"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        private async Task<string> ValidateStageAsync(string walletAddress, string publicKey, StageSetting stageSetting, SubmitOrderUpdate update)
        {
            var stage = update.SelectedStage;
            var monthCount = update.MonthDuration;

            if (monthCount < 1) throw new BadRequestException("Minimum of the duration for payoff is 1 month");

            if (monthCount > stageSetting.MaximumPayOffMonth)
                throw new BadRequestException($"Max of Payoff month is {stageSetting.MaximumPayOffMonth} in {stage.ToDisplay()} stage");


            var userStages = await _userStageService.GetUserStagesByWalletAddressForInternalUsage(walletAddress);
            var userStageType = userStages.FirstOrDefault(q => q.Stage == update.SelectedStage)
                ?? throw new BadRequestException($"The {stage.ToDisplay()} stage is not active for user!");

            return userStageType.UserStageId;
        }


        /// <summary>
        /// this method use for get sum of final loan amount of user in specific stage
        /// </summary>
        /// <param name="publicKey"></param>
        /// <param name="walletAddress"></param>
        /// <param name="stage"></param>
        /// <returns></returns>
        private async Task<decimal> GetFrozenUsingLoanAmountAsync(string publicKey, string walletAddress, UserStageType stage)
        {
            var result = await _orderRepository.AsQueryable()
                .Where(q => q.State == OrderState.Registered)
                .Where(q => q.UserPublicKey == publicKey && q.WalletAddress == walletAddress && q.Stage == stage)
                .SumAsync(q => q.LoanAmount);

            return result;
        }


        /// <summary>
        /// this method use for calculate loan amount
        /// with token amount(quantity) find effective price
        /// then calculate the loan amount (effective price * amount)
        /// validate the loanAmount availability and user frozen
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <param name="publicKey"></param>
        /// <param name="stageSetting"></param>
        /// <param name="update"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        private async Task<(decimal, EffectivePriceResult)> CalculateTokenAmountAsync(
            string walletAddress, string publicKey, StageSetting stageSetting, SubmitOrderUpdate update)
        {
            var tokenName = update.TokenName;
            var tokenQuantity = await _pancakeSwapService.GetOptimalSwapAmountInBSCAsync(new _PancakeSwap.DTOs.GetSwapAmountUpdate
            {
                USDTAmount = update.USDTAmount,
                TokenName = tokenName
            });

            var stage = update.SelectedStage;

            await ValidateExistingTokenWithQuantityAsync(tokenName, tokenQuantity);

            var effectivePrice = await _priceService.CalculateEffectivePriceAsync(tokenName, tokenQuantity, update.USDTAmount);
            var loanAmount = update.USDTAmount;

            var (minBuy, maxBuy) = _userStageService.GetMinAndMaxBuyAmountWithStage(walletAddress, stage);


            if (loanAmount < minBuy) throw new BadRequestException($"Min of buy amount is {minBuy}");
            if (loanAmount > maxBuy) throw new BadRequestException($"Max of buy amount is {maxBuy}");

            var frozenLoanAmount = await GetFrozenUsingLoanAmountAsync(publicKey, walletAddress, stage);
            var remainForSubmitOrder = maxBuy - frozenLoanAmount;
            if (loanAmount > remainForSubmitOrder)
                throw new BadRequestException($"Your remain loan amount for submit order is {remainForSubmitOrder} in {stage.ToDisplay()} stage");
            return (tokenQuantity, effectivePrice);
        }


        /// <summary>
        /// use for loan amount profit
        /// calculate with selected stage setting and given month duration count
        /// find one month profit and * duration
        /// </summary>
        /// <param name="stageSetting"></param>
        /// <param name="monthDuration"></param>
        /// <param name="loanAmount"></param>
        /// <returns></returns>
        private decimal CalculateLoanAmountInterest(StageSetting stageSetting, int monthDuration, decimal loanAmount)
        {
            var profitPercentage = stageSetting.ProfitPercentage;
            var profit = ((loanAmount * profitPercentage) / 100) * monthDuration;
            return profit;
        }


        /// <summary>
        /// use for check token available quantity
        /// check with initial quantity and sum off using quantity in orders(paid and register orders)
        /// </summary>
        /// <param name="tokenName"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        private async Task ValidateExistingTokenWithQuantityAsync(string tokenName, decimal quantity)
        {
            var inventoryAvailableQuantity = await GetOneInventoryQuantityByTokenNameAsync(tokenName);
            if (quantity > inventoryAvailableQuantity)
                throw new BadRequestException($"available quantity of {tokenName} is {inventoryAvailableQuantity}");
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


        /// <summary>
        /// for find inventory by token name
        /// throw error if not found
        /// </summary>
        /// <param name="tokenName"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        private async Task<decimal> GetOneInventoryQuantityByTokenNameAsync(string tokenName)
        {
            var inventoryBalance = await _inventoryService.GetQuantityAsyncGetOneByTokenNameForInternalUsageAsync(tokenName);

            if (inventoryBalance <= 0) throw new BadRequestException("there is no quantity for token inventory!");
            var usedQuantity = await GetSumOfPaidAndRegisteredOrderedTokenAsync(tokenName);

            var availavleQuantity = inventoryBalance - usedQuantity;
            if (availavleQuantity <= 0) throw new BadRequestException("Quantity of token if full!");

            return availavleQuantity;
        }


        /// <summary>
        /// use for get real quantity of token
        /// </summary>
        /// <param name="tokenName"></param>
        /// <returns></returns>
        private async Task<decimal> GetSumOfPaidAndRegisteredOrderedTokenAsync(string tokenName)
        {
            return await _orderRepository.AsQueryable()
                .Where(q => q.TokenName.Equals(tokenName, StringComparison.CurrentCultureIgnoreCase))
                .Where(q => q.State == OrderState.Registered)
                .SumAsync(q => q.TokenAmount);
        }

       

        #endregion

    }
}

//private async Task SyncOrdersWithCampaignAsync(List<Order> orders)
//{
//    foreach (var order in orders)
//    {
//        if (order.State != OrderState.Registered)
//            continue;

//        if (!string.IsNullOrEmpty(order.CampaignReference))
//            continue;

//        var campaign = await _campaignService.GetBestCampaignAsync(
//            order.WalletAddress,
//            order.OrderId,
//            order.CreatedMoment);

//        if (campaign == null)
//            continue;

//        order.CampaignReference = campaign.CampaignReference;
//        order.CampaignDiscount = campaign.DiscountPercentage;

//        var orderCampaignFilter = Builders<Order>.Filter.Eq(
//         x => x.Id,
//         order.Id);

//        var orderCampaignUpdate = Builders<Order>.Update
//            .Set(x => x.CampaignReference, campaign.CampaignReference)
//            .Set(x => x.CampaignDiscount, campaign.DiscountPercentage);

//        await _orderRepository.FindOneAndUpdateAsync(orderCampaignFilter, orderCampaignUpdate);
//    }
//}






  ///// <summary>
        ///// this method use for get all of orders for user
        ///// </summary>
        ///// <param name="pagination"></param>
        ///// <param name="userPublicKey"></param>
        ///// <param name="walletAddress"></param>
        ///// <returns></returns>
        //public async Task<OrderListResult> GetAllUserOrdersAsync(GetAllUserOrdersUpdate update, string userPublicKey, string walletAddress)
        //{
        //    var rzusdPrice = await _priceService.GetRZUSDPriceAsync();

        //    var pagination = update.Pagination;

        //    var skip = (pagination.Page - 1) * pagination.Size;

        //    var query = _orderRepository.AsQueryable();

        //    if (update.States != null && update.States.Count > 0)
        //    {
        //        query = query.Where(q => update.States.Contains(q.State));
        //    }

        //    var totalCount = await query
        //        .CountAsync(x => x.WalletAddress.ToLower() == walletAddress.ToLower());

        //    var orders = await query
        //     .Where(x => x.WalletAddress.ToLower() == walletAddress.ToLower())
        //     .OrderByDescending(x => x.CreatedMoment)
        //     .Skip(skip)
        //     .Take(pagination.Size)
        //     .ToListAsync();

        //    await SyncOrdersWithCampaignAsync(orders);

        //    var result = orders.Select(order => new OrderResult
        //    {
        //        CreatedMoment = order.CreatedMoment,
        //        ModifiedMoment = order.ModifiedMoment,
        //        OrderId = order.OrderId,
        //        State = order.State,
        //        WalletAddress = walletAddress,
        //        ChangeStateMoment = order.ChangeStateMoment,
        //        FinalAmount = order.FinalAmount,
        //        LoanAmount = order.LoanAmount,
        //        LoanInterestAmount = order.LoanInterestAmount,
        //        MonthDuration = order.MonthDuration,
        //        PayOffDate = order.PayOffDate,
        //        ProfitRatePerMonth = order.ProfitRatePerMonth,
        //        Promotion = order.Promotion,
        //        Quantity = order.TokenAmount,
        //        Stage = order.Stage,
        //        TokenAddress = order.TokenAddress,
        //        TokenEffectivePrice = order.TokenEffectivePrice,
        //        TokenName = order.TokenName,
        //        TokenNetwork = order.TokenNetwork,
        //        UserStageId = order.UserStageId,

        //        // convert to rzusd
        //        PayAmountInWei = order.State == OrderState.Registered
        //        ? _blockChainService
        //            .ConvertToWei(
        //                ((decimal)order.FinalAmount *
        //                 (1 - ((order.CampaignDiscount ?? 0) / 100)))
        //                 / rzusdPrice)
        //            .ToString()
        //        : order.PayAmountInWei,

        //        TokenAmountInWei = order.TokenAmountInWei,
        //        TransactionsMetaData = order.TransactionsMetaData,
        //        CampaignDiscount = order.CampaignDiscount,
        //    }).ToList();

        //    var pageCount = (int)Math.Ceiling((double)totalCount / pagination.Size);

        //    return new OrderListResult
        //    {
        //        Data = result,
        //        TotalCount = totalCount,
        //        PageCount = pageCount
        //    };
        //}


