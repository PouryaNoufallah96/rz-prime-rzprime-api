using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories;
using RZPrime.Domain.Repositories.Contracts;
using RZPrime.Services._TransactionLog.DTOs.Updates;
using System.Numerics;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._TransactionLog
{
    public class TransactionLogService(
        IOrderRepository _orderRepository,
        IHubContext<PaidOrderHub> _hubContext,
        ITransactionLogRepository _transactionLogRepository,

        ILogger<TransactionLogRepository> _logger) : ITransactionLogService, IScopedDependency
    {

        public async Task CreateOrderRegisteredTransactionLogAsync(OrderRegisteredLogData log)
        {
            var existsLog = await _transactionLogRepository.AsQueryable()
                    .Where(q =>
                        q.Hash.ToLower() == log.Hash.ToLower() &&
                        q.OrderId.ToLower() == log.OrderId.ToLower() &&
                        q.EventType == BlockchainEventType.OrderRegistered)
                    .FirstOrDefaultAsync();

            if (existsLog != null)
            {
                _logger.LogWarning(
                    "Duplicate OrderRegistered log detected for OrderId {OrderId}. Skipping insertion. Hash: {Hash}",
                    log.OrderId, log.Hash);
                return;
            }

            var discountPercentage = (decimal)log.DiscountBps / 100m;

            var newLog = new TransactionLog
            {
                Address = log.Address,
                OrderId = log.OrderId,
                UserWallet = log.UserWallet,
                TokenName = "",
                TokenAmount = log.TokenAmount.ToString(),
                USDTAmount = "",
                Hash = log.Hash,
                BlockNumber = log.BlockNumber,
                CampaignReference = log.CampaignId,
                Discount = discountPercentage,
                EventType = BlockchainEventType.OrderRegistered,
                Status = TransactionStatus.Confirmed
            };

            await _transactionLogRepository.InsertOneAsync(newLog);

            if(discountPercentage > 0)
            {
                await _orderRepository.FindOneAndUpdateAsync(
                    filter: x => x.OrderId == log.OrderId,
                    update: Builders<Order>.Update
                        .Set(x => x.CampaignReference, log.CampaignId)
                        .Set(x => x.CampaignDiscount, discountPercentage)
                );
            }
        }

        public async Task CreateOrderExecutedTransactionLogAsync(OrderExecutedLogData log)
        {
            var existsLog = await _transactionLogRepository.AsQueryable()
                  .Where(q =>
                      q.Hash.ToLower() == log.Hash.ToLower() &&
                      q.OrderId.ToLower() == log.OrderId.ToLower() &&
                      q.EventType == BlockchainEventType.OrderExecuted)
                  .FirstOrDefaultAsync();

            if (existsLog != null)
            {
                _logger.LogWarning(
                    "Duplicate OrderExecuted log detected for OrderId {OrderId}. Skipping insertion. Hash: {Hash}",
                    log.OrderId, log.Hash);
                return;
            }


            var newLog = new TransactionLog
            {
                Address = log.Address,
                OrderId = log.OrderId,
                UserWallet = log.UserWallet,
                TokenName = "RZUSD",
                TokenAmount = log.RzusdPaid.ToString(),
                USDTAmount = log.UsdValue.ToString(),
                Hash = log.Hash,
                BlockNumber = log.BlockNumber,
                EventType = BlockchainEventType.OrderExecuted,
                Status = TransactionStatus.Confirmed
            };

            await _transactionLogRepository.InsertOneAsync(newLog);

            var filter = Builders<Order>.Filter.Eq(o => o.OrderId, log.OrderId);

            var txHash = log.Hash;
            var rzusdAmount = ConvertFromWei(log.RzusdPaid);
            var now = DateTime.UtcNow;

            var newMetaData = new OrderTransactionMeta
            {
                CreateMoment = now,
                Hash = log.Hash,
                Status = TransactionStatus.Confirmed,
                PayAmountInRZUSD = rzusdAmount
            };

            var update = Builders<Order>.Update
                .Set(o => o.State, OrderState.Paid)
                .Set(o => o.ChangeStateMoment, now)
                .Push(o => o.TransactionsMetaData, newMetaData);

            var updatedOrder = await _orderRepository.FindOneAndUpdateWithOptionAsync(filter, update);
            var order = updatedOrder ?? await GetOrderAsync(log.OrderId);

            try
            {

                var shortHash = txHash.Length > 10 ? txHash[..10] : txHash;
                await _hubContext.Clients.Group(order.WalletAddress).SendAsync("NotifyPaidOrder", $"Transaction {shortHash}... is completed successfully");

            }
            catch (Exception ex)
            {
                _logger.LogError($"error in sending hub execute order : {order.Id} -- message =>>> {ex.Message}");
            }
        }

        public async Task CreateOrderExpiredTransactionLogAsync(OrderExpiredLogData log)
        {

            var existsLog = await _transactionLogRepository.AsQueryable()
                .Where(q =>
                    q.Hash.ToLower() == log.Hash.ToLower() &&
                    q.OrderId.ToLower() == log.OrderId.ToLower() &&
                    q.EventType == BlockchainEventType.OrderExpired)
                .FirstOrDefaultAsync();

            if (existsLog != null)
            {
                _logger.LogWarning(
                    "Duplicate OrderExpired log detected for OrderId {OrderId}. Skipping insertion. Hash: {Hash}",
                    log.OrderId, log.Hash);
                return;
            }

            var newLog = new TransactionLog
            {
                Address = log.Address,
                OrderId = log.OrderId,
                UserWallet = log.UserWallet,
                TokenName = "RZUSD",
                TokenAmount = "0",
                USDTAmount ="0",
                Hash = log.Hash,
                BlockNumber = log.BlockNumber,
                EventType = BlockchainEventType.OrderExpired,
                Status = TransactionStatus.Confirmed
            };

            await _transactionLogRepository.InsertOneAsync(newLog);



            var txHash = log.Hash;
            var now = DateTime.UtcNow;
            var filter = Builders<Order>.Filter.Eq(o => o.OrderId, log.OrderId);

            var newMetaData = new OrderTransactionMeta
            {
                CreateMoment = now,
                Hash = txHash,
                Status = TransactionStatus.Expired,
                PayAmountInRZUSD = 0
            };

            var update = Builders<Order>.Update
                .Set(o => o.State, OrderState.Drop)
                .Set(o => o.ChangeStateMoment, now)
                .Push(o => o.TransactionsMetaData, newMetaData);

            var updatedOrder = await _orderRepository.FindOneAndUpdateWithOptionAsync(filter, update);
            var order = updatedOrder ?? await GetOrderAsync(log.OrderId);
            try
            {

                var shortHash = txHash.Length > 10 ? txHash[..10] : txHash;
                await _hubContext.Clients.Group(order.WalletAddress).SendAsync("NotifyPaidOrder", $"Transaction {shortHash}... is expired");

            }
            catch (Exception ex)
            {
                _logger.LogError($"error in sending hub execute order : {order.Id} -- message =>>> {ex.Message}");
            }
        }

        public async Task<BigInteger> GetLastCheckedBlockNumberAsync()
        {
            var lastBlock = await _transactionLogRepository
             .AsQueryable()
              .Where(q => q.EventType == BlockchainEventType.OrderRegistered)
             .OrderByDescending(b => b)
             .FirstOrDefaultAsync();

            if (lastBlock == null)
            {
                return new BigInteger(0);
            }

            return new BigInteger(lastBlock.BlockNumber);
        }

        private async Task<Order> GetOrderAsync(string orderId)
        {
            var order = await _orderRepository.FindOneAsync(q => q.OrderId == orderId);
            return order;
        }
       
        private decimal ConvertFromWei(BigInteger weiAmount, int decimals = 18)
        {
            if (weiAmount < 0) throw new ArgumentException("Amount must be a positive integer.");
            var factor = (decimal)BigInteger.Pow(10, decimals);
            return (decimal)weiAmount / factor;
        }

    }
}
