using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.ABI.CompilationMetadata;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Util;
using Nethereum.Web3;
using RZPrime.Domain.Collections;
using RZPrime.DTOs.Contracts;
using RZPrime.Services._BlockChainWebSocket.DTOs;
using RZPrime.Services._Inventory;
using RZPrime.Services._Price.DTOs.Settings;
using RZPrime.Services._TransactionLog;
using RZPrime.Services._TransactionLog.DTOs;
using System.Net.WebSockets;
using System.Numerics;
using System.Reactive.Linq;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._BlockChainWebSocket._BackgroundService
{
    public class BlockchainEventBackgroundService : BackgroundService, IHostedDependency
    {
        private readonly ILogger<BlockchainEventBackgroundService> _logger;
        private readonly BlockchainWebSocketSetting _settings;
        private readonly IBlockChainInventory _inventoryService;
        private readonly AvailableTokensSettings _availableTokensSettings;
        private readonly ITransactionLogService _transactionLogService;
        private Web3 _web3;
        private StreamingWebSocketClient _webSocketClient;
        private readonly SemaphoreSlim _reconnectLock = new SemaphoreSlim(1, 1);
        private bool _isDisposed = false;
        private int _reconnectAttempts = 0;
        private IDisposable _transactionSubscription;
        private IDisposable _contractEventsSubscription;
        private IDisposable _incomingTransferSubscription;
        private BigInteger _lastProcessedBlock = 0;
        private readonly object _blockLock = new object();
        private bool _useSecondaryWsUrl = false;
        private DateTime _lastEventReceived = DateTime.UtcNow;
        public BlockchainEventBackgroundService(
            ILogger<BlockchainEventBackgroundService> logger,
            BlockchainWebSocketSetting settings,
            IBlockChainInventory inventoryService,
            AvailableTokensSettings availableTokensSettings,
            ITransactionLogService transactionLogService)
        {
            _logger = logger;
            _settings = settings;
            _inventoryService = inventoryService;
            _availableTokensSettings = availableTokensSettings;
            _transactionLogService = transactionLogService;

            _web3 = new Web3(settings.WsUrl);
            //_contract = _web3.Eth.GetContract(ContractAbi, _settings.ContractAddress);
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Blockchain Event Service starting...");
            _lastProcessedBlock = await _transactionLogService.GetLastCheckedBlockNumberAsync();
            _logger.LogInformation($"starting block is : {_lastProcessedBlock}");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                   
                    await TryConnectWithRetryAsync(stoppingToken);
                    _logger.LogInformation("-----------------------Successfully connected and subscribed to blockchain events");

                    while (_webSocketClient?.IsStarted == true && !stoppingToken.IsCancellationRequested)
                    {
                        if ((DateTime.UtcNow - _lastEventReceived).TotalMinutes > 3)
                        {
                            _logger.LogWarning("No blockchain events received in the last 3 minutes. Reconnecting...");
                            await TryConnectWithRetryAsync(stoppingToken);
                        }

                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }
                    _logger.LogWarning(" WebSocket stopped unexpectedly, reconnecting...");
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

        private async Task TryConnectWithRetryAsync(CancellationToken stoppingToken)
        {

            await _reconnectLock.WaitAsync(stoppingToken);
            try
            {
                _reconnectAttempts = 0;

                while (!stoppingToken.IsCancellationRequested &&
                       _reconnectAttempts < _settings.MaxReconnectAttempts)
                {
                    try
                    {
                        _logger.LogInformation($"Attempting to connect (Attempt {_reconnectAttempts + 1}/{_settings.MaxReconnectAttempts})");
                        await ConnectAndSubscribe(stoppingToken);

                        _reconnectAttempts = 0;
                        return;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _reconnectAttempts++;
                        _logger.LogWarning(ex, "Connection attempt failed. Will retry...");
                        await Task.Delay(CalculateReconnectDelay(), stoppingToken);
                    }
                }

                if (_reconnectAttempts >= _settings.MaxReconnectAttempts)
                {
                    _logger.LogCritical("Max reconnection attempts reached. Waiting before next try...");
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
            double delaySeconds = Math.Min(
                Math.Pow(2, _reconnectAttempts) * _settings.ReconnectInterval,
                300);
            return TimeSpan.FromSeconds(delaySeconds);
        }

        private async Task ConnectAndSubscribe(CancellationToken cancellationToken)
        {
            _logger.LogInformation("...........ConnectAndSubscribe touched............");

           
           await CleanupConnection();

            var currestWsUrl = GetCurrentWsUrl();
            _webSocketClient = new StreamingWebSocketClient(currestWsUrl);
            _web3 = new Web3(currestWsUrl);

            
            try
            {
                await _webSocketClient.StartAsync();

              
                await SubscribeToContractEventsAsync(cancellationToken);

                //// Subscribe to incoming transfers
                //await SubscribeToTransactionConfirmationsAsync();
                //_logger.LogInformation("Incoming confirm subscription active.");


                await SubscribeToIncomingTransfersAsync(cancellationToken);

                _logger.LogInformation("All subscriptions active.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting/subscribing. Will reconnect...");
                throw;
            }
        }

        private string GetCurrentWsUrl()
        {
            var wss = _useSecondaryWsUrl ? _settings.WsUrl2 : _settings.WsUrl;
            _useSecondaryWsUrl = !_useSecondaryWsUrl;
            _logger.LogInformation("WebSocket URL : {Url}", wss);
            return wss;
        }

        private async Task CleanupConnection()
        {
            try
            {
                if (_webSocketClient != null)
                {
                    await _webSocketClient.StopAsync();
                    _webSocketClient.Dispose();
                }
                _contractEventsSubscription?.Dispose();
                //_transactionSubscription?.Dispose();
                _incomingTransferSubscription?.Dispose();

                _contractEventsSubscription = null;
                //_transactionSubscription = null;
                _incomingTransferSubscription = null;
                _webSocketClient = null;

                _logger.LogInformation("Cleaned up previous connections/subscriptions.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup.");
            }
        }

        private async Task SubscribeToContractEventsAsync(CancellationToken cancellationToken)
        {
            var subscription = new EthLogsObservableSubscription(_webSocketClient);

            var safeObservable = subscription.GetSubscriptionDataResponsesAsObservable()
           .Where(log => log.Address.IsTheSameAddress(_settings.ContractAddress))
           .Select(log => Observable.FromAsync(() => ProcessContractEventLogAsync(log)))
           .Concat();

            _contractEventsSubscription = safeObservable.Subscribe(
                _ => { },
                async ex =>
                {
                    _logger.LogError(ex, "Error in subscription. Reconnecting...");
                    _ = Task.Run(async () => await TryConnectWithRetryAsync(cancellationToken));
                },
                () =>
                {
                    _logger.LogWarning("Subscription completed unexpectedly. Reconnecting...");
                    _ = Task.Run(async () => await TryConnectWithRetryAsync(cancellationToken));
                });

            var filter = new NewFilterInput
            {
                Address = new[] { _settings.ContractAddress },
                FromBlock = new BlockParameter(GetLastProcessedBlock())
            };

            await subscription.SubscribeAsync(filter);
        }

        private HexBigInteger GetLastProcessedBlock()
        {
            lock (_blockLock)
            {
                return _lastProcessedBlock.ToHexBigInteger();
            }
        }

        private async Task ProcessContractEventLogAsync(FilterLog log)
        {
            try
            {
                var orderRegistered = log.DecodeEvent<OrderRegisteredEventDTO>();
                if (orderRegistered != null)
                {
                    _logger.LogInformation("OrderRegistered: {OrderId} by {User}",
                        orderRegistered.Event.OrderId, orderRegistered.Event.User);
                    return;
                }

                var orderExecuted = log.DecodeEvent<OrderExecutedEventDTO>();
                if (orderExecuted != null)
                {
                    _logger.LogInformation("OrderExecuted: {OrderId} by {User}",
                        orderExecuted.Event.OrderId, orderExecuted.Event.User);

                    await LogOrderExecutedEvent(orderExecuted, log);

                    _lastProcessedBlock = BigInteger.Max(_lastProcessedBlock, log.BlockNumber.Value + 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decoding blockchain event");
            }
        }
        
        private async Task SubscribeToIncomingTransfersAsync(CancellationToken cancellationToken) 
        {
            var tokenAddresses = _availableTokensSettings
                .Select(t => t.Address.ToLower())
                .ToList();

            var subscription = new EthLogsObservableSubscription(_webSocketClient);

            _incomingTransferSubscription = subscription.GetSubscriptionDataResponsesAsObservable()
                .Subscribe(
                    async log =>
                    {
                        try
                        {
                            if (tokenAddresses.Contains(log.Address.ToLower()))
                            {
                                var transferEvent = log.DecodeEvent<TransferEventDTO>();
                                if (transferEvent != null && transferEvent.Event.To.IsTheSameAddress(_settings.ContractAddress))
                                {
                                    var token = _availableTokensSettings.FirstOrDefault(t => t.Address.IsTheSameAddress(log.Address));
                                    var amount = Web3.Convert.FromWei(transferEvent.Event.Value);
                                    _logger.LogInformation("Incoming {Token} Transfer: {Amount} from {From}", token.Name, amount, transferEvent.Event.From);

                                    await _inventoryService.SyncInventoryQuantityAsync(token.Name.ToUpper());
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error processing incoming token transfer {ex.Message}");
                        }
                    },
                    async ex =>
                    {
                        _logger.LogError(ex, "Error in incoming transfer subscription. Reconnecting...");
                        _ = Task.Run(async () => await TryConnectWithRetryAsync(cancellationToken));
                    },
                    () =>
                    {
                        _logger.LogWarning("Incoming transfer subscription completed unexpectedly. Reconnecting...");
                        _ = Task.Run(async () => await TryConnectWithRetryAsync(cancellationToken));
                    }
                );

            var filter = new NewFilterInput
            {
                Address = tokenAddresses.Concat(new[] { _settings.ContractAddress }).ToArray()
            };

            await subscription.SubscribeAsync(filter);
        }
        //private async Task SubscribeToIncomingTransfersAsync(CancellationToken cancellationToken)
        //{
        //    var tokenAddresses = _availableTokensSettings
        //        .Select(t => t.Address.ToLower())
        //        .ToList();

        //    var subscription = new EthLogsObservableSubscription(_webSocketClient);

        //    var safeObservable = subscription.GetSubscriptionDataResponsesAsObservable()
        //        .Where(log => tokenAddresses.Contains(log.Address.ToLower()))
        //        .Select(log => Observable.FromAsync(() => ProcessIncomingTransferLogAsync(log)))
        //        .Concat();

        //    _incomingTransferSubscription = safeObservable.Subscribe(
        //        _ => { },
        //        async ex =>
        //        {
        //            _logger.LogError(ex, "Error in incoming transfer subscription. Reconnecting...");
        //            await TryConnectWithRetryAsync(cancellationToken);
        //        },
        //        () =>
        //        {
        //            _logger.LogWarning("Incoming transfer subscription completed unexpectedly. Reconnecting...");
        //            _ = Task.Run(async () => await TryConnectWithRetryAsync(cancellationToken));
        //        });

        //    var filter = new NewFilterInput
        //    {
        //        Address = tokenAddresses.Concat(new[] { _settings.ContractAddress }).ToArray()
        //    };

        //    await subscription.SubscribeAsync(filter);
        //}

        //private async Task ProcessIncomingTransferLogAsync(FilterLog log)
        //{
        //    try
        //    {
        //        var transferEvent = log.DecodeEvent<TransferEventDTO>();
        //        if (transferEvent != null && transferEvent.Event.To.IsTheSameAddress(_settings.ContractAddress))
        //        {
        //            var token = _availableTokensSettings.FirstOrDefault(t => t.Address.IsTheSameAddress(log.Address));
        //            var amount = Web3.Convert.FromWei(transferEvent.Event.Value);

        //            _logger.LogInformation("Incoming {Token} Transfer: {Amount} from {From}", token.Name, amount, transferEvent.Event.From);

        //            await _inventoryService.SyncInventoryQuantityAsync(token.Name.ToUpper());
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error processing incoming token transfer");
        //    }
        //}

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
                    Amount = Web3.Convert.FromWei(eventLog.Event.PayAmount),
                }
            };

            lock (_blockLock)
            {
                _lastProcessedBlock = BigInteger.Max(_lastProcessedBlock, block);
            }

            await _transactionLogService.CreateOrderExecutedTransactionLogAsync(transactionLog);
            _lastEventReceived = DateTime.UtcNow;
            _logger.LogInformation("Logged OrderExecuted event for order {OrderId}", eventLog.Event.OrderId);
        }

        private async Task SubscribeToTransactionConfirmationsAsync()
        {
            var blockSubscription = new EthNewBlockHeadersObservableSubscription(_webSocketClient);

            var safeObservable = blockSubscription.GetSubscriptionDataResponsesAsObservable()
                .SelectMany(blockHeader => Observable.FromAsync(() =>
                    _web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                        .SendRequestAsync(new BlockParameter(blockHeader.Number))
                ))
                .Where(block => block?.Transactions != null)
                .SelectMany(block => block.Transactions
                    .Where(tx => tx.To != null && tx.To.IsTheSameAddress(_settings.ContractAddress))
                    .Select(tx => Observable.FromAsync(() => ProcessTransactionConfirmed(tx)))
                )
                .Concat();

            _transactionSubscription = safeObservable.Subscribe(
                _ => { },
                ex => _logger.LogError(ex, "Error in transaction subscription"),
                () => _logger.LogInformation("Transaction subscription completed")
            );

            await blockSubscription.SubscribeAsync();
        }

        private async Task ProcessTransactionConfirmed(Transaction transaction)
        {
            _logger.LogInformation(
                "Processing confirmed transaction: {TxHash} in block #{BlockNumber}...",
                transaction.TransactionHash,
                transaction.BlockNumber.Value);

            try
            {
                var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction.TransactionHash);

                if (receipt == null)
                {
                    _logger.LogWarning(
                        "Could not retrieve transaction receipt for {TxHash}.",
                        transaction.TransactionHash);
                    return;
                }

                if (receipt.Status.Value == 0)
                {
                    return;
                }

                _logger.LogInformation("Attempting to extract OrderId from receipt for {TxHash}...", transaction.TransactionHash);
                string orderId = ExtractOrderIdFromReceipt(receipt);

                if (!string.IsNullOrEmpty(orderId))
                {
                    _logger.LogInformation(
                        "Successfully extracted OrderId '{OrderId}' from transaction {TxHash}.",
                        orderId,
                        transaction.TransactionHash);


                    var log = new ConfirmTxLog
                    {
                        OrderId = orderId,
                        ConfirmData =
                        new()
                        {
                            From = transaction.From,
                            To = transaction.To,
                            Hash = transaction.TransactionHash,
                            BlockNumber = (long)receipt.BlockNumber.Value,
                            Status = TransactionStatus.Confirmed,
                            EventType = BlockchainEventType.OrderExecuted
                        }

                    };
                    await _transactionLogService.CreateOrderConfirmedTransactionLogAsync(log);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while processing transaction {TxHash}.", transaction.TransactionHash);
            }
        }

        private string ExtractOrderIdFromReceipt(TransactionReceipt receipt)
        {
            try
            {
                if (receipt?.Logs == null || !receipt.Logs.Any())
                {
                    return null;
                }

                var orderExecutedEvents = receipt.DecodeAllEvents<OrderExecutedEventDTO>();
                if (orderExecutedEvents.Any())
                {
                    return orderExecutedEvents.First().Event.OrderId;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while trying to extract OrderId from transaction receipt with hash: {TxHash}", receipt?.TransactionHash);
                return null;
            }
        }

       



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


    }
}


//private async Task HandleDisconnection(CancellationToken stoppingToken)
//{
//    if (stoppingToken.IsCancellationRequested) return;

//    CleanupConnection();

//    if (_reconnectAttempts < _settings.MaxReconnectAttempts)
//    {
//        await HandleConnectionFailure(stoppingToken);
//    }
//}
//private async Task HandleConnectionFailure(CancellationToken stoppingToken)
//{
//    if (_reconnectAttempts >= _settings.MaxReconnectAttempts)
//    {
//        _logger.LogCritical("Max reconnection attempts reached. Service will stop.");
//        return;
//    }

//    var delay = CalculateReconnectDelay();
//    _logger.LogWarning($"Connection failed. Will retry in {delay.TotalSeconds} seconds...");
//    await Task.Delay(delay, stoppingToken);
//}


//protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//{
//    _logger.LogInformation("Blockchain Event Service starting...");

//    int reconnectAttempts = 0;

//    while (!stoppingToken.IsCancellationRequested)
//    {
//        try
//        {
//            await ConnectAndSubscribe(stoppingToken);

//            reconnectAttempts = 0;
//        }
//        catch (OperationCanceledException)
//        {
//            _logger.LogInformation("Blockchain Service is stopping...");
//            break;
//        }
//        catch (Exception ex)
//        {
//            reconnectAttempts++;
//            int delaySeconds = Math.Min(30, 5 * reconnectAttempts); 

//            _logger.LogError(ex, "Error in blockchain main loop. Reconnecting in {Delay}s...", delaySeconds);

//            CleanupConnection();

//            try
//            {
//                await Task.Delay(delaySeconds * 1000, stoppingToken);
//            }
//            catch (OperationCanceledException)
//            {
//                break;
//            }
//        }
//    }

//    _logger.LogInformation("Blockchain Event Service stopped.");
//}

//private string ExtractOrderIdFromReceipt(TransactionReceipt receipt)
//{
//    try
//    {
//        if (receipt?.Logs == null || !receipt.Logs.Any())
//        {
//            return null;
//        }

//        var orderExecutedEvents = receipt.DecodeAllEvents<OrderExecutedEventDTO>();
//        if (orderExecutedEvents.Any())
//        {
//            return orderExecutedEvents.First().Event.OrderId;
//        }

//        var orderRegisteredEvents = receipt.DecodeAllEvents<OrderRegisteredEventDTO>();
//        if (orderRegisteredEvents.Any())
//        {
//            return orderRegisteredEvents.First().Event.OrderId;
//        }

//        return null;
//    }
//    catch (Exception ex)
//    {
//        _logger.LogError(ex, "Error occurred while trying to extract OrderId from transaction receipt with hash: {TxHash}", receipt?.TransactionHash);
//        return null;
//    }
//}
