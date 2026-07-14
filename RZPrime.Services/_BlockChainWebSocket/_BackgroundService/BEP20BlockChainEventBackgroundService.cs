using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Util;
using Nethereum.Web3;
using RZPrime.Domain.Collections;
using RZPrime.Services._BlockChain.DTOs.Settings;
using RZPrime.Services._BlockChainWebSocket.DTOs;
using RZPrime.Services._Inventory;
using RZPrime.Services._Price.DTOs.Settings;
using RZPrime.Services._TransactionLog;
using RZPrime.Services._TransactionLog.DTOs.Updates;
using System.Numerics;
using System.Reactive.Linq;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._BlockChainWebSocket._BackgroundService
{
    public class BEP20BlockChainEventBackgroundService : BackgroundService, IHostedDependency
    {
        private const string TransferLogPrefix = "[BEP20-WS-Transfer]";
        private const string OrderLogPrefix = "[BEP20-WS-ORDER]";
        private const string CommonLogPrefix = "[BEP20-WS]";

        private readonly BlockChainSettings _blockChainSettings;
        private readonly ITransactionLogService _transactionLogService;
        private readonly IBlockChainInventory _blockChainInventory;
        private readonly ILogger<BEP20BlockChainEventBackgroundService> _logger;
        private readonly AvailableTokensSettings _availableTokensSettings;

        private Web3 _web3;
        private StreamingWebSocketClient _webSocketClient;

        private readonly string[] _rpcUrls;
        private readonly string[] _wsUrls;

        private int _currentRpcIndex = 0;
        private int _currentWsIndex = 0;

        private readonly object _blockLock = new();

        private BigInteger _orderLastProcessedBlock = 0;


        private readonly SemaphoreSlim _reconnectLock = new(1, 1);
        private readonly SemaphoreSlim _cleanupLock = new(1, 1);

        private IDisposable _orderContractEventsSubscription;
        private IDisposable _incomingTransferSubscription;


        private int _reconnectAttempts = 0;
        private DateTime _lastEventReceived = DateTime.UtcNow;

        private bool _isDisposed = false;

        public BEP20BlockChainEventBackgroundService(
            BlockChainSettings blockChainSettings,
            ITransactionLogService transactionLogService,
            IBlockChainInventory blockChainInventory,
            AvailableTokensSettings availableTokensSettings,
            ILogger<BEP20BlockChainEventBackgroundService> logger)
        {
            _blockChainSettings = blockChainSettings;
            _transactionLogService = transactionLogService;
            _blockChainInventory = blockChainInventory;
            _availableTokensSettings = availableTokensSettings;
            _logger = logger;

            _rpcUrls = new[] { _blockChainSettings.RpcUrl, _blockChainSettings.RpcUrl2 };
            _wsUrls = new[] { _blockChainSettings.WsUrl, _blockChainSettings.WsUrl2 };

            InitializeClients();
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TryConnectWithRetryAsync(stoppingToken);
                    _lastEventReceived = DateTime.UtcNow;

                    //_logger.LogInformation("{Prefix} Connected to BEP20 blockchain", LogPrefix);

                    while (_webSocketClient?.IsStarted == true && !stoppingToken.IsCancellationRequested)
                    {
                        if ((DateTime.UtcNow - _lastEventReceived).TotalMinutes > 2)
                        {
                            //_logger.LogWarning("{Prefix} No BEP20 events received. Reconnecting...", LogPrefix);

                            await Task.Delay(2000, stoppingToken);
                            await TryConnectWithRetryAsync(stoppingToken);

                            _lastEventReceived = DateTime.UtcNow;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }

                    await Task.Delay(2000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("{Prefix} Shutdown requested", CommonLogPrefix);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{Prefix} Unexpected error", CommonLogPrefix);
                    SwitchRpc();
                    SwitchWs();
                    InitializeClients();
                    await Task.Delay(5000, stoppingToken);
                }
            }

            _logger.LogInformation("{Prefix} Service stopped", CommonLogPrefix);
        }

        private async Task TryConnectWithRetryAsync(CancellationToken stoppingToken)
        {
            if (!await _reconnectLock.WaitAsync(0, stoppingToken))
            {
                return;
            }

            try
            {
                _reconnectAttempts = 0;

                while (!stoppingToken.IsCancellationRequested && _reconnectAttempts < Math.Max(1, _blockChainSettings.MaxReconnectAttempts))
                {
                    try
                    {
                        await ConnectAndSubscribe(stoppingToken);
                        _reconnectAttempts = 0;
                        return;
                    }
                    catch (Exception ex)
                    {
                        _reconnectAttempts++;

                        _logger.LogWarning(ex, "{Prefix} Connection failed. Retry: {Retry}", CommonLogPrefix, _reconnectAttempts);

                        SwitchRpc();
                        SwitchWs();
                        InitializeClients();

                        await Task.Delay(CalculateReconnectDelay(), stoppingToken);
                    }
                }

                if (_reconnectAttempts >= Math.Max(1, _blockChainSettings.MaxReconnectAttempts))
                {
                    await Task.Delay(30000, stoppingToken);
                    _reconnectAttempts = 0;
                }
            }
            finally
            {
                _reconnectLock.Release();
            }
        }

        private TimeSpan CalculateReconnectDelay()
        {
            var interval = Math.Max(1, _blockChainSettings.ReconnectInterval);
            var delaySeconds = Math.Min(Math.Pow(2, _reconnectAttempts) * interval, 300);
            return TimeSpan.FromSeconds(delaySeconds);
        }

        private async Task ConnectAndSubscribe(CancellationToken cancellationToken)
        {
            await CleanupConnection();

            var wsUrl = GetCurrentWsUrl();
            _webSocketClient = new StreamingWebSocketClient(wsUrl);
            _web3 = new Web3(GetCurrentRpcUrl());

            try
            {
                await _webSocketClient.StartAsync();

                await SubscribeToOrderContractEventsAsync(cancellationToken);
                await SubscribeToIncomingTransfersAsync(cancellationToken);

                _logger.LogInformation("{Prefix} Subscriptions active", CommonLogPrefix);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} ConnectAndSubscribe failed", CommonLogPrefix);
                throw;
            }
        }

        private async Task CleanupConnection()
        {
            if (!await _cleanupLock.WaitAsync(0))
            {
                _logger.LogInformation("Cleanup already in progress, skipping...");
                return;
            }

            try
            {
                _logger.LogInformation("Starting cleanup...");

                _orderContractEventsSubscription?.Dispose();
                _incomingTransferSubscription?.Dispose();
                _orderContractEventsSubscription = null;
                _incomingTransferSubscription = null;

                if (_webSocketClient != null)
                {
                    try
                    {
                        if (_webSocketClient.IsStarted)
                        {
                            await _webSocketClient.StopAsync();
                            await Task.Delay(300);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "WebSocket StopAsync failed or already stopped");
                    }

                    try
                    {
                        _webSocketClient.Dispose();
                    }
                    catch (SemaphoreFullException ex)
                    {
                        _logger.LogWarning(ex, "Ignoring SemaphoreFullException from WebSocket.Dispose()");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "WebSocket Dispose failed");
                    }

                    _webSocketClient = null;
                }

                _logger.LogInformation("Cleanup completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup.");
            }
            finally
            {
                _cleanupLock.Release();
            }
        }

        private void InitializeClients()
        {
            _web3 = new Web3(GetCurrentRpcUrl());
        }

        private string GetCurrentRpcUrl()
        {
            return _rpcUrls[_currentRpcIndex];
        }

        private string GetCurrentWsUrl()
        {
            return _wsUrls[_currentWsIndex];
        }

        private void SwitchRpc()
        {
            _currentRpcIndex = (_currentRpcIndex + 1) % _rpcUrls.Length;
        }

        private void SwitchWs()
        {
            _currentWsIndex = (_currentWsIndex + 1) % _wsUrls.Length;
        }




        #region Order
        private async Task SubscribeToOrderContractEventsAsync(CancellationToken cancellationToken)
        {
            var subscription = new EthLogsObservableSubscription(_webSocketClient);

            var safeObservable = subscription.GetSubscriptionDataResponsesAsObservable()
           .Where(log => log.Address.IsTheSameAddress(_blockChainSettings.ContractAddress))
           .Select(log => Observable.FromAsync(() => OrderProcessContractEventLogAsync(log, cancellationToken)))
           .Concat();

            _orderContractEventsSubscription = safeObservable.Subscribe(
                _ => { },
                async ex =>
                {
                    _logger.LogError(ex, "{Prefix} Order subscription error", OrderLogPrefix);
                },
                () =>
                {
                    _logger.LogWarning("{Prefix} Order subscription completed", OrderLogPrefix);
                });

            var filter = new NewFilterInput
            {
                Address = new[] { _blockChainSettings.ContractAddress },
                FromBlock = new BlockParameter(await GetOrderLastProcessedBlock(cancellationToken))
            };

            await subscription.SubscribeAsync(filter);
        }

        private async Task OrderProcessContractEventLogAsync(FilterLog log, CancellationToken cancellationToken)
        {
            try
            {
                _lastEventReceived = DateTime.UtcNow;

                var orderRegistered = log.DecodeEvent<OrderRegisteredEventDTO>();
                if (orderRegistered != null)
                {
                    _logger.LogInformation("OrderRegistered: {OrderId} by {User}",
                        orderRegistered.Event.OrderId, orderRegistered.Event.User);

                    SentrySdk.CaptureMessage(
                        $"OrderRegistered: {orderRegistered.Event.OrderId} by {orderRegistered.Event.User}"
                    );
                    return;
                }

                var orderExecuted = log.DecodeEvent<OrderExecutedEventDTO>();
                if (orderExecuted != null)
                {
                    _logger.LogInformation("OrderExecuted: {OrderId} by {User}",
                        orderExecuted.Event.OrderId, orderExecuted.Event.User);

                    SentrySdk.CaptureMessage(
                        $"OrderExecuted: {orderExecuted.Event.OrderId} by {orderExecuted.Event.User}"
                    );

                    await LogOrderExecutedEvent(orderExecuted, log);

                    _orderLastProcessedBlock = BigInteger.Max(_orderLastProcessedBlock, log.BlockNumber.Value + 1);
                }

                var orderExpired = log.DecodeEvent<OrderExpiredEventDTO>();
                if (orderExpired != null)
                {
                    _logger.LogInformation("OrderExpired: {OrderId} by {User}",
                        orderExpired.Event.OrderId, orderExpired.Event.User);

                    SentrySdk.CaptureMessage(
                        $"OrderExpired: {orderExpired.Event.OrderId} by {orderExpired.Event.User}"
                    );

                    await LogOrderExpiredEvent(orderExpired, log);

                    _orderLastProcessedBlock = BigInteger.Max(_orderLastProcessedBlock, log.BlockNumber.Value + 1);
                }


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decoding blockchain event");
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

            lock (_blockLock)
            {
                _orderLastProcessedBlock = BigInteger.Max(_orderLastProcessedBlock, block);
            }

            await _transactionLogService.CreateOrderExecutedTransactionLogAsync(transactionLog);
            _lastEventReceived = DateTime.UtcNow;
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
            _lastEventReceived = DateTime.UtcNow;
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

        #endregion


        #region TrasferSide

        private async Task SubscribeToIncomingTransfersAsync(CancellationToken cancellationToken)
        {
            var tokenAddresses = _availableTokensSettings
                .Select(t => t.Address.ToLower())
                .ToList();

            var subscription = new EthLogsObservableSubscription(_webSocketClient);

            var safeObservable = subscription.GetSubscriptionDataResponsesAsObservable()
                .Where(log => tokenAddresses.Contains(log.Address.ToLower()))
                .Select(log => Observable.FromAsync(() => ProcessIncomingTransferLogAsync(log)))
                .Concat();

            var contractAddresses = new List<string>
            {
                _blockChainSettings.ContractAddress
            };


            _incomingTransferSubscription = safeObservable.Subscribe(
                _ => { },
                async ex =>
                {
                    _logger.LogError(ex, "{Prefix} Incoming transfer subscription error", TransferLogPrefix);
                },
                () =>
                {
                    _logger.LogWarning("{Prefix} Incoming transfer subscription completed", TransferLogPrefix);
                });

            var filter = new NewFilterInput
            {
                Address = tokenAddresses.Concat(contractAddresses).ToArray()
            };

            await subscription.SubscribeAsync(filter);
        }

        private async Task ProcessIncomingTransferLogAsync(FilterLog log)
        {
            try
            {
                _lastEventReceived = DateTime.UtcNow;

                var transferEvent = log.DecodeEvent<TransferEventDTO>();
                if (transferEvent == null) return;

                var to = transferEvent.Event.To;
                var token = _availableTokensSettings.FirstOrDefault(t => t.Address.IsTheSameAddress(log.Address));

                if (token == null)
                {
                    _logger.LogWarning("{Prefix} Token not found: {Address}", TransferLogPrefix, log.Address);
                    return;
                }

                var amount = Web3.Convert.FromWei(transferEvent.Event.Value);

                if (to.IsTheSameAddress(_blockChainSettings.ContractAddress))
                {

                    _logger.LogInformation("Incoming {Token} Transfer: {Amount} from {From}", token.Name, amount, transferEvent.Event.From);
                    await _blockChainInventory.SyncInventoryQuantityAsync(token.Name.ToUpper());

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix} Error processing incoming token transfer", TransferLogPrefix);
            }
        }

        #endregion

     
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_isDisposed) return;

            _logger.LogInformation("Shutting down blockchain event service...");

            try
            {
                await CleanupConnection();
            }
            finally
            {
                _isDisposed = true;
                await base.StopAsync(cancellationToken);
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _orderContractEventsSubscription?.Dispose();
                _incomingTransferSubscription?.Dispose();
                _webSocketClient?.Dispose();
                _reconnectLock?.Dispose();
                _cleanupLock?.Dispose();
                _isDisposed = true;
            }
        }
    }
}
