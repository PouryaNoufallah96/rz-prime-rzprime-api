using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RZPrime.Domain.Collections;
using RZPrime.Services._BlockChain.DTOs.Settings;
using RZPrime.Services._BlockChainWebSocket.DTOs;
using RZPrime.Services._TransactionLog;
using RZPrime.Services._TransactionLog.DTOs.Updates;
using System.Numerics;
using System.Reactive.Linq;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._BlockChainWebSocket._BackgroundService
{
    public class BEP20PollingEventBackgroundService : BackgroundService, IHostedDependency
    {
        private const string OrderLogPrefix = "[BEP20-POLLING-ORDER]";
        private const string CommonLogPrefix = "[BEP20-POLLING]";

        private readonly ITransactionLogService _transactionLogService;
        private readonly ILogger<BEP20PollingEventBackgroundService> _logger;
        private readonly BlockChainSettings _blockChainSettings;
        private readonly object _blockLock = new();

        private Web3 _web3;

        private readonly string[] _rpcUrls;

        private int _currentRpcIndex = 0;

        private BigInteger _orderLastProcessedBlock = 0;

        private readonly string _orderContractAddress;


        private bool _isDisposed = false;

        public BEP20PollingEventBackgroundService(
            ITransactionLogService transactionLogService,
            ILogger<BEP20PollingEventBackgroundService> logger,
            BlockChainSettings blockChainSettings)
        {
            _transactionLogService = transactionLogService;
            _logger = logger;
            _blockChainSettings = blockChainSettings;

            _orderContractAddress = _blockChainSettings.ContractAddress;

            _rpcUrls = new[] { _blockChainSettings.RpcUrl, _blockChainSettings.RpcUrl2 };

            InitializeClients();
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    BigInteger latestBlock = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                    _logger.LogInformation(
                     "{Prefix} Checking latest block: {Block}",
                     CommonLogPrefix,
                     latestBlock);

                    var safeBlock = latestBlock - 10;

                    await PollOrderMissingLogsAsync(safeBlock, stoppingToken);

                    await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Service shutdown requested");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Prefix} Unexpected error in blockchain event service", CommonLogPrefix);
                    await Task.Delay(5000, stoppingToken);
                }
            }

            _logger.LogInformation("Blockchain Event Service stopped.");
        }



        #region Order

        private async Task PollOrderMissingLogsAsync(BigInteger latestBlock, CancellationToken cancellationToken)
        {

            if (_orderLastProcessedBlock < 1)
            {
                _orderLastProcessedBlock = await GetOrderLastProcessedBlock(cancellationToken);
            }

            if (_orderLastProcessedBlock >= latestBlock) return;

            const int blockChunk = 2000;
            BigInteger fromBlock = _orderLastProcessedBlock;


            while (fromBlock <= latestBlock)
            {
                BigInteger toBlock = BigInteger.Min(fromBlock + blockChunk - 1, latestBlock);

                var filter = new NewFilterInput
                {
                    FromBlock = new BlockParameter(new HexBigInteger(fromBlock)),
                    ToBlock = new BlockParameter(new HexBigInteger(toBlock)),
                    Address = new[] { _orderContractAddress }
                };

                try
                {
                    var logs = await _web3.Eth.Filters.GetLogs.SendRequestAsync(filter);

                    foreach (var log in logs)
                    {
                        var filterLog = log as FilterLog;
                        if (filterLog == null) continue;

                        try
                        {
                            var orderRegistered = log.DecodeEvent<OrderRegisteredEventDTO>();
                            if (orderRegistered != null)
                            {
                                await LogOrderRegisteredEvent(orderRegistered, log);

                                SentrySdk.CaptureMessage(
                                    $"OrderRegistered: {orderRegistered.Event.OrderId} by {orderRegistered.Event.User}"
                                );

                                continue;
                            }

                            var orderExecuted = log.DecodeEvent<OrderExecutedEventDTO>();
                            if (orderExecuted != null)
                            {
                                await LogOrderExecutedEvent(orderExecuted, log);

                                SentrySdk.CaptureMessage(
                                    $"OrderExecuted: {orderExecuted.Event.OrderId} by {orderExecuted.Event.User}"
                                );

                                continue;
                            }

                            var orderExpired = log.DecodeEvent<OrderExpiredEventDTO>();
                            if (orderExpired != null)
                            {

                                await LogOrderExpiredEvent(orderExpired, log);

                                SentrySdk.CaptureMessage(
                                    $"OrderExpired: {orderExpired.Event.OrderId} by {orderExpired.Event.User}"
                                );

                                continue;
                            }

                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "{Prefix} Error decoding polled log", OrderLogPrefix);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Prefix} Error polling logs from {FromBlock} to {ToBlock}", OrderLogPrefix, fromBlock, toBlock);
                }

                fromBlock = toBlock + 1;
                await Task.Delay(3000, cancellationToken);
            }

            lock (_blockLock)
            {
                _orderLastProcessedBlock = BigInteger.Max(_orderLastProcessedBlock, latestBlock);
                _logger.LogInformation(
                 "{Prefix} Checking latest block: {Block}",
                 CommonLogPrefix,
                 latestBlock);
            }
        }

        private async Task LogOrderRegisteredEvent(EventLog<OrderRegisteredEventDTO> eventLog, FilterLog log)
        {
            try
            {
                var campaignIdHex = ByteArray32ToHex(eventLog.Event.CampaignId);
                var block = (long)log.BlockNumber.Value;

                var hasCampaign = !string.IsNullOrEmpty(campaignIdHex)
                    && campaignIdHex != "0x0000000000000000000000000000000000000000000000000000000000000000"
                    && eventLog.Event.DiscountBps > 0;

                _logger.LogInformation(
                   "OrderRegistered event logged successfully. OrderId: {OrderId}, User: {User}, TokenAmount: {TokenAmount}, CampaignId: {CampaignId}, DiscountBps: {DiscountBps}, HasCampaign: {HasCampaign}, BlockNumber: {BlockNumber}, TxHash: {TxHash}",
                   eventLog.Event.OrderId,
                   eventLog.Event.User,
                   eventLog.Event.TokenAmount,
                   campaignIdHex ?? "None",
                   eventLog.Event.DiscountBps,
                   hasCampaign,
                   block,
                   log.TransactionHash);

                var transactionLog = new OrderRegisteredLogData
                {
                    OrderId = eventLog.Event.OrderId,
                    Hash = log.TransactionHash,
                    Address = log.Address,
                    UserWallet = eventLog.Event.User,
                    Status = TransactionStatus.Confirmed,
                    BlockNumber = block,
                    EventType = BlockchainEventType.OrderRegistered,
                    TokenAmount = eventLog.Event.TokenAmount,
                    CampaignId = hasCampaign ? campaignIdHex : null,
                    DiscountBps = hasCampaign ? eventLog.Event.DiscountBps : 0
                };

                await _transactionLogService.CreateOrderRegisteredTransactionLogAsync(transactionLog);

                lock (_blockLock)
                {
                    _orderLastProcessedBlock = BigInteger.Max(_orderLastProcessedBlock, block);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OrderRegistered event for order {OrderId}. TxHash: {TxHash}",
                    eventLog.Event.OrderId, log.TransactionHash);
                throw;
            }
        }

        private async Task LogOrderExecutedEvent(EventLog<OrderExecutedEventDTO> eventLog, FilterLog log)
        {

            _logger.LogInformation("Logged OrderExecuted event for order {OrderId}", eventLog.Event.OrderId);

            var block = (long)log.BlockNumber.Value;
            var transactionLog = new OrderExecutedLogData
            {
                OrderId = eventLog.Event.OrderId,
                Hash = log.TransactionHash,
                Address = log.Address,
                UserWallet = eventLog.Event.User,
                Status = TransactionStatus.Confirmed,
                BlockNumber = (long)log.BlockNumber.Value,
                EventType = BlockchainEventType.OrderExecuted,
                RzusdPaid = eventLog.Event.RzusdPaid,
                UsdValue = 0,
            };

            await _transactionLogService.CreateOrderExecutedTransactionLogAsync(transactionLog);
        }

        private async Task LogOrderExpiredEvent(EventLog<OrderExpiredEventDTO> eventLog, FilterLog log)
        {

            _logger.LogInformation("Logged OrderExpired event for order {OrderId}", eventLog.Event.OrderId);

            var block = (long)log.BlockNumber.Value;
            var transactionLog = new OrderExpiredLogData
            {
                OrderId = eventLog.Event.OrderId,
                Hash = log.TransactionHash,
                Address = log.Address,
                UserWallet = eventLog.Event.User,
                Status = TransactionStatus.Confirmed,
                BlockNumber = (long)log.BlockNumber.Value,
                EventType = BlockchainEventType.OrderExpired,
            };

            await _transactionLogService.CreateOrderExpiredTransactionLogAsync(transactionLog);
        }

        private async Task<HexBigInteger> GetOrderLastProcessedBlock(CancellationToken cancellationToken)
        {
            try
            {
                lock (_blockLock)
                {
                    if (_orderLastProcessedBlock > 0)
                        return _orderLastProcessedBlock.ToHexBigInteger();
                }

                var lastDbBlock = await _transactionLogService.GetLastCheckedBlockNumberAsync();

                lock (_blockLock)
                {
                    _orderLastProcessedBlock = lastDbBlock;
                }

                if (_orderLastProcessedBlock > 0)
                    return _orderLastProcessedBlock.ToHexBigInteger();

                try
                {
                    var _web3Client = new Web3(_blockChainSettings.RpcUrl);
                    var latestBlockNumber = await _web3Client.Eth.Blocks.GetBlockNumber.SendRequestAsync();

                    lock (_blockLock)
                    {
                        _orderLastProcessedBlock = latestBlockNumber;
                        return latestBlockNumber;
                    }

                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                    throw;
                }

            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                throw;
            }
        }

        private static string ByteArray32ToHex(byte[] bytes)
        {
            try
            {
                if (bytes == null)
                    throw new ArgumentNullException(nameof(bytes));

                if (bytes.Length != 32)
                    throw new ArgumentException("Input must be exactly 32 bytes for bytes32");

                return "0x" + bytes.ToHex();
            }
            catch (Exception)
            {
                return null;
            }
        }
        #endregion


        private void InitializeClients()
        {
            _web3 = new Web3(GetCurrentRpcUrl());
        }

        private string GetCurrentRpcUrl()
        {
            return _rpcUrls[_currentRpcIndex];
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_isDisposed) return;

            _logger.LogInformation("{Prefix} Stopping polling service...", CommonLogPrefix);

            _isDisposed = true;
            await base.StopAsync(cancellationToken);
        }
    }
}
