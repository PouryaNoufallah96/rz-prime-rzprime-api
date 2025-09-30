using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RZPrime.Domain.Collections;
using RZPrime.Domain.Repositories;
using RZPrime.Domain.Repositories.Contracts;
using RZPrime.Services._TransactionLog.DTOs;
using RZPrime.Services._TransactionLog.DTOs.Results;
using RZPrime.Utilities.DTOs;
using RZPrime.Utilities.Exceptions.Common;
using System.Numerics;
using MongoDB.Driver.Linq;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._TransactionLog
{
    public class TransactionLogService(
        IOrderRepository _orderRepository,
        IHubContext<PaidOrderHub> _hubContext,
        ITransactionLogRepository _transactionLogRepository,

        ILogger<TransactionLogRepository> _logger) : ITransactionLogService, IScopedDependency
    {

        //public async Task CreateOrderRegisteredTransactionLogAsync(RegisteredTxLog log)
        //{

        //    try
        //    {
        //        var filter = Builders<TransactionLog>.Filter.Eq(x => x.OrderId, log.OrderId);
        //        var update = Builders<TransactionLog>.Update
        //            .SetOnInsert(x => x.TokenName, log.TokenName)
        //            .SetOnInsert(x => x.OrderId, log.OrderId)
        //            .SetOnInsert(x => x.TokenAmount, log.TokenAmount)
        //            .SetOnInsert(x => x.USDTAmount, log.USDTAmount)
        //            .SetOnInsert(x => x.UserWallet, log.UserWallet)
        //            .SetOnInsert(x => x.Histories, new List<TransactionLogHistory>())
        //            .AddToSet(x => x.Histories, log.RegisteredData);

        //        await _transactionLogRepository.UpsertOneAsync(filter, update);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error creating transaction log");
        //        throw;
        //    }

        //}

        public async Task CreateOrderRegisteredTransactionLogAsync(RegisteredTxLog log)
        {
            //_logger.LogInformation($"order register orderId{transactionLog.OrderId} {DateTime.UtcNow}");

            try
            {
                var exlog = await _transactionLogRepository.FindOneAsync(q => q.OrderId == log.OrderId);
                if (exlog != null)
                {
                    if (exlog.Histories.Any(q => q.Hash == log.RegisteredData.Hash && q.Status == TransactionStatus.Confirmed))
                    {
                        exlog.UserWallet = log.UserWallet;
                        exlog.TokenName = log.TokenName;
                        exlog.TokenAmount = log.TokenAmount;
                        exlog.USDTAmount = log.USDTAmount;
                        await _transactionLogRepository.ReplaceOneAsync(exlog);
                    }
                }
                else
                {
                    var newLog = new TransactionLog
                    {
                        TokenName = log.TokenName,
                        OrderId = log.OrderId,
                        TokenAmount = log.TokenAmount,
                        USDTAmount = log.USDTAmount,
                        UserWallet = log.UserWallet,
                        Histories = [log.RegisteredData]
                    };

                    await _transactionLogRepository.InsertOneAsync(newLog);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transaction log");
                throw;
            }
        }

        public async Task CreateOrderExecutedTransactionLogAsync(ExecutedTxLog log)
        {
            try
            {
                var exlog = await _transactionLogRepository.FindOneAsync(q => q.OrderId == log.OrderId);
                if (exlog != null)
                {
                    var txHash = log.ExecuteData.Hash;

                    if (exlog.Histories.Select(q => q.Hash).Contains(txHash))
                    {
                        _logger.LogInformation($"Duplicated hash : {txHash}");
                        return;
                    }
                      
                    exlog.Histories.Add(log.ExecuteData);
                    await _transactionLogRepository.ReplaceOneAsync(exlog);

                    var now = DateTime.UtcNow;
                    var filter = Builders<Order>.Filter.Eq(o => o.OrderId, log.OrderId);


                    var newMetaData = new OrderTransactionMeta
                    {
                        CreateMoment = now,
                        Hash = txHash,
                        Status = TransactionStatus.Confirmed
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
                else
                {
                    var newLog = new TransactionLog
                    {
                        OrderId = log.OrderId,
                        IsError = true,
                        Histories = [log.ExecuteData]
                    };
                    await _transactionLogRepository.InsertOneAsync(newLog);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transaction execute log");
                throw;
            }

        }

        public async Task CreateOrderConfirmedTransactionLogAsync(ConfirmTxLog log)
        {
            //_logger.LogInformation($"===================================================");
            //_logger.LogInformation($"Order confirmed orderId{log.OrderId} {DateTime.UtcNow}");

            try
            {
                var exlog = await _transactionLogRepository
                    .FindOneAsync(q => q.OrderId == log.OrderId);

                if (exlog != null)
                {
                    var txHash = log.ConfirmData.Hash;
                    //var isExecuteHhistory = exlog.Histories.Any(q => q.Hash != txHash);

                    exlog.Histories.Add(log.ConfirmData);
                    await _transactionLogRepository.ReplaceOneAsync(exlog);

                    //if (isExecuteHhistory)
                    //{
                    //    var now = DateTime.UtcNow;

                    //    var filter = Builders<Order>.Filter.Eq(o => o.OrderId, log.OrderId);


                    //    var newMetaData = new OrderTransactionMeta
                    //    {
                    //        CreateMoment = now,
                    //        Hash = txHash,
                    //        Status = TransactionStatus.Confirmed
                    //    };

                    //    var update = Builders<Order>.Update
                    //        .Set(o => o.State, OrderState.Paid)
                    //        .Set(o => o.ChangeStateMoment, now)
                    //        .Push(o => o.TransactionsMetaData, newMetaData);

                        
                    //    var updatedOrder = await _orderRepository.FindOneAndUpdateWithOptionAsync(filter, update);
                    //    var order = updatedOrder ?? await GetOrderAsync(log.OrderId);

                    //    try
                    //    {
                    //        var shortHash = txHash.Length > 10 ? txHash[..10] : txHash;
                    //        await _hubContext.Clients.Group(order.WalletAddress).SendAsync("NotifyPaidOrder", $"Transaction {shortHash}... is completed successfully");
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        _logger.LogError($"error in sending hub confirmed order : {order.Id} -- message =>>> {ex.Message}");
                    //    }

                    //}
                
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transaction log");
                throw;
            }


        }

        public async Task CreateOrderFailedTransactionLogAsync(FailTxLog log)
        {

            //_logger.LogInformation($"===================================================");
            //_logger.LogInformation($"Order failed orderId{log.OrderId} {DateTime.UtcNow}");

            try
            {
                var txHash = log.FailData.Hash;
                var exlog = await _transactionLogRepository
                    .FindOneAsync(q => q.OrderId == log.OrderId);

                if (exlog != null)
                {
                    var isExecuteHhistory = exlog.Histories.Any(q => q.Hash != txHash);

                    exlog.Histories.Add(log.FailData);
                    await _transactionLogRepository.ReplaceOneAsync(exlog);

                    if (isExecuteHhistory)
                    {
                        var now = DateTime.UtcNow;

                        var filter = Builders<Order>.Filter.Eq(o => o.OrderId, log.OrderId);

                        var newMetaData = new OrderTransactionMeta
                        {
                            CreateMoment = now,
                            Hash = txHash,
                            Status = TransactionStatus.Failed
                        };

                        var update = Builders<Order>.Update
                            .Set(o => o.State, OrderState.Registered)
                            .Set(o => o.ChangeStateMoment, now)
                            .Push(o => o.TransactionsMetaData, newMetaData);

                        var updatedOrder = await _orderRepository.FindOneAndUpdateWithOptionAsync(filter, update);
                        var order = updatedOrder ?? await GetOrderAsync(log.OrderId);

                        try
                        {
                            var shortHash = txHash.Length > 10 ? txHash[..10] : txHash;
                            await _hubContext.Clients.Group(order.WalletAddress).SendAsync("NotifyPaidOrder", $"Transaction {shortHash}... is Failed");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"error in sending hub confirmed message =>>> {ex.Message}");
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating transaction log for failed transaction - hash : {log.FailData.Hash}");
                throw;
            }



        }

        public async Task<IEnumerable<RZPrime.Domain.Collections.TransactionLog>> GetByOrderIdAsync(string orderId)
        {
            try
            {
                return await _transactionLogRepository.FilterByAsync(
                    x => x.OrderId == orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions by order ID");
                throw;
            }
        }

        public Task<TransactionListResult> ListTransactionsAsync(Pagination pagination, string walletAddress)
        {
            throw new NotImplementedException();
        }

        public async Task<BigInteger> GetLastCheckedBlockNumberAsync()
        {
            var lastBlock = await _transactionLogRepository
             .AsQueryable()
             .SelectMany(t => t.Histories.Select(h => h.BlockNumber))
             .OrderByDescending(b => b)
             .FirstOrDefaultAsync();

            return new BigInteger(lastBlock);
        }


        private async Task<Order> GetOrderAsync(string orderId)
        {
            var order = await _orderRepository.FindOneAsync(q => q.OrderId == orderId);
            return order;
        }

        public async Task<TransactionLog> GetOneTransactionLogWithOrderIdAndWalletAsync(string orderId, string walletAddress)
        {
            var transactionLog = await _transactionLogRepository.FindOneAsync(q => q.OrderId == orderId && q.UserWallet == walletAddress) ??
               throw new NotFoundException("registered order not found!");
            return transactionLog;
        }
    }
}
