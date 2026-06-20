using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RZPrime.Domain.Collections;
using RZPrime.Services._BlockChain.DTOs.Settings;
using RZPrime.Services._BlockChainWebSocket.DTOs;
using RZPrime.Services._TransactionLog;
using RZPrime.Services._TransactionLog.DTOs;
using System.Numerics;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._BlockChainWebSocket._BackgroundService
{
    public class PollingEventBackgroundService(ITransactionLogService _transactionLogService,
        ILogger<PollingEventBackgroundService> _logger,
        BlockChainSettings _settings) : BackgroundService, IHostedDependency
    {
        private BigInteger _lastProcessedBlock = 0;
        private readonly object _blockLock = new object();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("------------------ Polling missing logs before subscription restart...");
            _lastProcessedBlock = await _transactionLogService.GetLastCheckedBlockNumberAsync();
            _logger.LogInformation($"starting block is : {_lastProcessedBlock}");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    //_logger.LogInformation("------------------ Polling missing logs before subscription restart...");
                    await PollMissingLogsAsync();

                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Service shutdown requested");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in blockchain event service");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            _logger.LogInformation("Blockchain Event Service stopped.");
        }


        private async Task PollMissingLogsAsync()
        {

            var _web3Client = new Web3(_settings.RpcUrl);
            BigInteger latestBlock = await GetLatestBlockSafeAsync(_web3Client);

            if (_lastProcessedBlock >= latestBlock) return;

            const int blockChunk = 2000;
            BigInteger fromBlock = GetLastProcessedBlock();

            while (fromBlock <= latestBlock)
            {
                BigInteger toBlock = BigInteger.Min(fromBlock + blockChunk - 1, latestBlock);

                var filter = new NewFilterInput
                {
                    FromBlock = new BlockParameter(new HexBigInteger(fromBlock)),
                    ToBlock = new BlockParameter(new HexBigInteger(toBlock)),
                    Address = new[] { _settings.ContractAddress }
                };

                try
                {
                    var logs = await _web3Client.Eth.Filters.GetLogs.SendRequestAsync(filter);

                    foreach (var log in logs)
                    {
                        var filterLog = log as FilterLog;
                        if (filterLog == null) continue;

                        try
                        {
                            var orderExecuted = filterLog.DecodeEvent<OrderExecutedEventDTO>();
                            if (orderExecuted != null)
                            {
                                await LogOrderExecutedEvent(orderExecuted, filterLog);
                                _logger.LogInformation("Polled Log saved to DB: TxHash={TxHash} , OrderId:{OrderId}", filterLog.TransactionHash, orderExecuted.Event.OrderId);
                            }
                            var orderExpired = filterLog.DecodeEvent<OrderExpiredEventDTO>();
                            if (orderExpired != null)
                            {
                                await LogOrderExpiredEvent(orderExpired, filterLog);
                                _logger.LogInformation("Polled Log saved to DB: TxHash={TxHash} , OrderId:{OrderId}", filterLog.TransactionHash, orderExpired.Event.OrderId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error decoding polled log");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling logs from {FromBlock} to {ToBlock}", fromBlock, toBlock);
                }

                fromBlock = toBlock + 1;
                await Task.Delay(3000);
            }
            lock (_blockLock)
            {
                _lastProcessedBlock = BigInteger.Max(_lastProcessedBlock, latestBlock);
                _logger.LogInformation("-------------- Poling until {latestBlock}", latestBlock);
            }
        }

        private async Task LogOrderExecutedEvent(EventLog<OrderExecutedEventDTO> eventLog, FilterLog log)
        {
            var block = (long)log.BlockNumber.Value;
            var transactionLog = new ExecutedTxLog
            {
                OrderId = eventLog.Event.OrderId,
                ExecuteData = new()
                {
                    Hash = log.TransactionHash,
                    From = log.Address,
                    To = eventLog.Event.User,
                    Status = TransactionStatus.Confirmed,
                    BlockNumber = (long)log.BlockNumber.Value,
                    EventType = BlockchainEventType.OrderExecuted,
                    Amount = Web3.Convert.FromWei(eventLog.Event.RzusdPaid),
                }
            };

            lock (_blockLock)
            {
                _lastProcessedBlock = BigInteger.Max(_lastProcessedBlock, block);
            }

            await _transactionLogService.CreateOrderExecutedTransactionLogAsync(transactionLog);
            _logger.LogInformation("Logged OrderExecuted event for order {OrderId}", eventLog.Event.OrderId);
        }

         private async Task LogOrderExpiredEvent(EventLog<OrderExpiredEventDTO> eventLog, FilterLog log)
        {

            var block = (long)log.BlockNumber.Value;
            var transactionLog = new ExpiredTxLog
            {
                OrderId = eventLog.Event.OrderId,
                ExpiredData = new()
                {
                    Hash = log.TransactionHash,
                    From = log.Address,
                    To = eventLog.Event.User,
                    Status = TransactionStatus.Confirmed,
                    BlockNumber = (long)log.BlockNumber.Value,
                    EventType = BlockchainEventType.OrderExpired,
                }
            };

            //lock (_blockLock)
            //{
            //    _lastProcessedBlock = BigInteger.Max(_lastProcessedBlock, block);
            //}

            await _transactionLogService.CreateOrderExpiredTransactionLogAsync(transactionLog);
            _logger.LogInformation("Logged OrderExpired event for order {OrderId}", eventLog.Event.OrderId);
        }

        private HexBigInteger GetLastProcessedBlock()
        {
            lock (_blockLock)
            {
                return _lastProcessedBlock.ToHexBigInteger();
            }
        }
      
        private async Task<BigInteger> GetLatestBlockSafeAsync(Web3 web3, int retries = 3)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    var result = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                    return (BigInteger)result.Value;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error getting latest block (attempt {Attempt})", i + 1);
                    await Task.Delay(2000);
                }
            }

            throw new Exception("Failed to retrieve latest block after retries");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Shutting down blockchain Polling service...");
            await base.StopAsync(cancellationToken);
        }


    }
}
