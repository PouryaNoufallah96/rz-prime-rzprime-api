using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.Contracts;
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
using System.Reactive.Linq;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._BlockChainWebSocket._BackgroundService
{
    public class BlockchainEventBackgroundService : BackgroundService, IHostedDependency
    {
        private const string ContractAbi = TokenForwardSaleAbi.Value;
        private readonly ILogger<BlockchainEventBackgroundService> _logger;
        private readonly BlockchainWebSocketSetting _settings;
        private readonly IBlockChainInventory _inventoryService;
        private readonly AvailableTokensSettings _availableTokensSettings;
        private readonly ITransactionLogService _transactionLogService;
        private Web3 _web3;
        private StreamingWebSocketClient _webSocketClient;
        private readonly Contract _contract;
        private int _reconnectAttempts = 0;
        private DateTime _lastConnectionTime = DateTime.MinValue;
        private bool _isDisposed = false;
        private IDisposable _transactionSubscription;
        private IDisposable _contractEventsSubscription;
        private IDisposable _incomingTransferSubscription;
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
            _contract = _web3.Eth.GetContract(ContractAbi, _settings.ContractAddress);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Blockchain Event Service starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TryConnectWithRetryAsync(stoppingToken);
                    await HandleDisconnection(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Service shutdown requested");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in blockchain event service");
                    await HandleDisconnection(stoppingToken);
                }
            }
        }

        private async Task TryConnectWithRetryAsync(CancellationToken stoppingToken)
        {
            _reconnectAttempts = 0;

            while (!stoppingToken.IsCancellationRequested &&
                   _reconnectAttempts < _settings.MaxReconnectAttempts)
            {
                try
                {
                    _logger.LogInformation($"Attempting to connect (Attempt {_reconnectAttempts + 1}/{_settings.MaxReconnectAttempts})");
                    await SubscribeToBlockchainEvents(stoppingToken);

                    _lastConnectionTime = DateTime.UtcNow;
                    _reconnectAttempts = 0;
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _reconnectAttempts++;
                    await HandleConnectionFailure(stoppingToken);
                }
            }
        }

        private async Task HandleConnectionFailure(CancellationToken stoppingToken)
        {
            if (_reconnectAttempts >= _settings.MaxReconnectAttempts)
            {
                _logger.LogCritical("Max reconnection attempts reached. Service will stop.");
                return;
            }

            var delay = CalculateReconnectDelay();
            _logger.LogWarning($"Connection failed. Will retry in {delay.TotalSeconds} seconds...");
            await Task.Delay(delay, stoppingToken);
        }

        private TimeSpan CalculateReconnectDelay()
        {
            double delaySeconds = Math.Min(
                Math.Pow(2, _reconnectAttempts) * _settings.ReconnectInterval,
                300);
            return TimeSpan.FromSeconds(delaySeconds);
        }

        private async Task HandleDisconnection(CancellationToken stoppingToken)
        {
            if (stoppingToken.IsCancellationRequested) return;

            CleanupConnection();

            if (_reconnectAttempts < _settings.MaxReconnectAttempts)
            {
                await HandleConnectionFailure(stoppingToken);
            }
        }

        private void CleanupConnection()
        {
            try
            {
                _transactionSubscription?.Dispose();
                _contractEventsSubscription?.Dispose();
                _webSocketClient?.Dispose();
                _incomingTransferSubscription?.Dispose();

                _transactionSubscription = null;
                _contractEventsSubscription = null;
                _webSocketClient = null;
                _incomingTransferSubscription = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during connection cleanup");
            }
        }

        private async Task SubscribeToBlockchainEvents(CancellationToken cancellationToken)
        {
            _webSocketClient = new StreamingWebSocketClient(_settings.WsUrl);
            await _webSocketClient.StartAsync();

            // Subscribe to contract events
            await SubscribeToContractEvents(cancellationToken);

            // Subscribe to transaction confirmations
            await SubscribeToTransactionConfirmations();

            await SubscribeToIncomingTransfers(cancellationToken);

            _logger.LogInformation("Started listening for blockchain events");

            // Keep the connection alive
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
            }

            UnsubscribeNetworkEvents();
        }

        private async Task SubscribeToContractEvents(CancellationToken cancellationToken)
        {
            var subscription = new EthLogsObservableSubscription(_webSocketClient);

            _contractEventsSubscription = subscription.GetSubscriptionDataResponsesAsObservable()
                .Where(log => log.Address.IsTheSameAddress(_settings.ContractAddress))
                .Subscribe(async log =>
                {
                    try
                    {
                        await ProcessBlockchainEvent(log);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing blockchain event");
                    }
                },
                ex => _logger.LogError(ex, "Error in blockchain event subscription"),
                () => _logger.LogInformation("Blockchain event subscription completed"));

            var filter = new NewFilterInput
            {
                Address = new[] { _settings.ContractAddress },
                FromBlock = BlockParameter.CreateLatest(),
                ToBlock = BlockParameter.CreateLatest()
            };

            await subscription.SubscribeAsync(filter);
        }

        private async Task SubscribeToTransactionConfirmations()
        {
            var blockSubscription = new EthNewBlockHeadersObservableSubscription(_webSocketClient);

            _transactionSubscription = blockSubscription.GetSubscriptionDataResponsesAsObservable()
                .Select(blockHeader =>
                {
                    return Observable.FromAsync(() =>
                        _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new BlockParameter(blockHeader.Number))
                    );
                })
                .Switch()
                .Where(block => block?.Transactions != null)
                .SelectMany(block => block.Transactions
                    .Where(tx => tx.To != null && tx.To.IsTheSameAddress(_settings.ContractAddress))
                )
                .Subscribe(async transaction =>
                {
                    try
                    {
                        await ProcessTransactionConfirmed(transaction);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing transaction confirmation for {TxHash}", transaction.TransactionHash);
                    }
                },
                ex => _logger.LogError(ex, "Error in transaction subscription"),
                () => _logger.LogInformation("Transaction subscription completed"));

            await blockSubscription.SubscribeAsync();
        }

        private async Task SubscribeToIncomingTransfers(CancellationToken cancellationToken)
        {
            var tokenAddresses = _availableTokensSettings
                .Select(t => t.Address.ToLower())
                .ToList();

            var subscription = new EthLogsObservableSubscription(_webSocketClient);

            _incomingTransferSubscription = subscription.GetSubscriptionDataResponsesAsObservable()
                .Subscribe(async log =>
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
                        _logger.LogError(ex, "Error processing incoming token transfer");
                    }

                }, ex => _logger.LogError(ex, "Error in incoming transfer subscription"),
                () => _logger.LogInformation("Incoming transfer subscription completed"));

            var filter = new NewFilterInput
            {
                Address = tokenAddresses.Concat(new[] { _settings.ContractAddress }).ToArray()
            };

            await subscription.SubscribeAsync(filter);
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
                    _logger.LogWarning(
                        "Transaction {TxHash} failed (reverted on-chain). Skipping.",
                        transaction.TransactionHash);

                    string failedOrderId = ExtractOrderIdFromReceipt(receipt);
                    var failLog = new FailTxLog
                    {
                        OrderId = failedOrderId ?? "failed" + Guid.NewGuid().ToString("N"),
                        FailData =
                       new()
                       {
                           From = transaction.From,
                           To = transaction.To,
                           Hash = transaction.TransactionHash,
                           BlockNumber = (long)receipt.BlockNumber.Value,
                           Status = TransactionStatus.Failed,
                           EventType = BlockchainEventType.TransactionFailed
                       }

                    };
                    await _transactionLogService.CreateOrderFailedTransactionLogAsync(failLog);
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


                    //var networkEvent = new NetworkEventLog
                    //{
                    //    EventType = BlockchainEventType.TransactionConfirmed,
                    //    Hash = transaction.TransactionHash,
                    //    BlockNumber = (long)receipt.BlockNumber.Value,
                    //    TimeStamp = DateTime.UtcNow,
                    //    Data = $"Transaction confirmed. " + (!string.IsNullOrEmpty(orderId) ? $"Associated OrderId: {orderId}" : "No associated order found.")
                    //};

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
                            EventType = BlockchainEventType.TransactionConfirmed
                        }

                    };
                    await _transactionLogService.CreateOrderConfirmedTransactionLogAsync(log);

                }
                else
                {
                    _logger.LogInformation(
                        "No relevant OrderId found in transaction {TxHash}. This may be an unrelated transaction.",
                        transaction.TransactionHash);
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

                var orderRegisteredEvents = receipt.DecodeAllEvents<OrderRegisteredEventDTO>();
                if (orderRegisteredEvents.Any())
                {
                    return orderRegisteredEvents.First().Event.OrderId;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while trying to extract OrderId from transaction receipt with hash: {TxHash}", receipt?.TransactionHash);
                return null;
            }
        }

        private async Task ProcessBlockchainEvent(FilterLog log)
        {
            try
            {
                var orderRegistered = log.DecodeEvent<OrderRegisteredEventDTO>();
                if (orderRegistered != null)
                {
                    await LogOrderRegisteredEvent(orderRegistered, log);
                    return;
                }

                var orderExecuted = log.DecodeEvent<OrderExecutedEventDTO>();
                if (orderExecuted != null)
                {
                    await LogOrderExecutedEvent(orderExecuted, log);
                    return;
                }

                _logger.LogWarning("Unknown event type received from blockchain");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decoding blockchain event");
            }
        }

        private async Task LogOrderRegisteredEvent(EventLog<OrderRegisteredEventDTO> eventLog, FilterLog log)
        {
            //var transactionLog = new TransactionLog
            //{
            //    OrderId = eventLog.Event.OrderId,
            //    UserWallet = eventLog.Event.User,
            //    TokenAmount = Web3.Convert.FromWei(eventLog.Event.TokenAmount),
            //    Hash = log.TransactionHash,
            //    Status = TransactionStatus.Pending,
            //    BlockNumber = (long)log.BlockNumber.Value,
            //    RawData = log.Data,
            //    EventType = BlockchainEventType.OrderRegistered,
            //    Amount = Web3.Convert.FromWei(eventLog.Event.TokenAmount)
            //};

            //await _transactionLogService.CreateOrderRegisteredTransactionLogAsync(transactionLog);
            //_logger.LogInformation("Logged OrderRegistered event for order {OrderId}", eventLog.Event.OrderId);
        }

        private async Task LogOrderExecutedEvent(EventLog<OrderExecutedEventDTO> eventLog, FilterLog log)
        {
            var transactionLog = new ExecutedTxLog
            {
                OrderId = eventLog.Event.OrderId,
                ExecuteData = new()
                {
                    Hash = log.TransactionHash,
                    From = log.Address,
                    To = eventLog.Event.User,
                    Status = TransactionStatus.Pending,
                    BlockNumber = (long)log.BlockNumber.Value,
                    EventType = BlockchainEventType.OrderExecuted,
                    Amount = Web3.Convert.FromWei(eventLog.Event.PayAmount),
                }
            };

            await _transactionLogService.CreateOrderExecutedTransactionLogAsync(transactionLog);
            _logger.LogInformation("Logged OrderExecuted event for order {OrderId}", eventLog.Event.OrderId);
        }


        private void UnsubscribeNetworkEvents()
        {
            _transactionSubscription?.Dispose();
            _contractEventsSubscription?.Dispose();

            _transactionSubscription = null;
            _contractEventsSubscription = null;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_isDisposed) return;

            _logger.LogInformation("Shutting down blockchain event service...");

            try
            {
                CleanupConnection();
            }
            finally
            {
                _isDisposed = true;
                await base.StopAsync(cancellationToken);
            }
        }


        //public string ExtractOrderIdFromTransactionInput(Transaction tx)
        //{
        //    try
        //    {
        //        // تعریف ABI تابع
        //        var functionAbi = new FunctionABI("executeOrder", false)
        //        {
        //            InputParameters = new[]
        //            {
        //                new Parameter("string", "orderId", 1)
        //            }
        //        };

        //        var decoder = new FunctionCallDecoder();
        //        var decoded = decoder.DecodeFunctionInput(functionAbi, tx.Input);

        //        var orderId = decoded["orderId"]?.ToString();
        //        return orderId;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to decode transaction input for TxHash {TxHash}", tx.TransactionHash);
        //        return null;
        //    }
        //}


    }
}

//private async Task SubscribeToNewBlocks()
//{
//    var blockSubscription = new EthNewBlockHeadersObservableSubscription(_webSocketClient);

//    _blockSubscription = blockSubscription.GetSubscriptionDataResponsesAsObservable()
//        .Subscribe(async blockHeader =>
//        {
//            try
//            {
//                await ProcessNewBlockMined(blockHeader);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error processing new block event");
//            }
//        },
//        ex => _logger.LogError(ex, "Error in block subscription"),
//        () => _logger.LogInformation("Block subscription completed"));

//    await blockSubscription.SubscribeAsync();
//}

//private async Task MonitorNetworkStatus(CancellationToken cancellationToken)
//{
//    while (!cancellationToken.IsCancellationRequested)
//    {
//        try
//        {
//            var syncing = await _web3.Eth.Syncing.SendRequestAsync();
//            await ProcessNetworkStatus(syncing);

//            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
//        }
//        catch (TaskCanceledException)
//        {
//            break;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error checking network status");
//            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
//        }
//    }
//}

//private async Task ProcessNetworkStatus(SyncingOutput syncingResult)
//{
//    string status;
//    if (syncingResult.IsSyncing)
//    {
//        status = $"Syncing - Current: {syncingResult.CurrentBlock}, Highest: {syncingResult.HighestBlock}";
//    }
//    else
//    {
//        status = "Fully synced";
//    }

//    var networkEvent = new NetworkEventLog
//    {
//        EventType = BlockchainEventType.NetworkStatus,
//        TimeStamp = DateTime.UtcNow,
//        Data = status
//    };

//    await _transactionLogService.CreateNetworkEventAsync(networkEvent);
//    _logger.LogInformation("Network status: {Status}", status);
//}

//private async Task ProcessNewBlockMined(Block blockHeader)
//{
//    var networkEvent = new NetworkEventLog
//    {
//        EventType = BlockchainEventType.BlockMined,
//        BlockNumber = (long)blockHeader.Number.Value,
//        Hash = blockHeader.BlockHash, // استفاده از BlockHash
//        TimeStamp = DateTime.UtcNow,
//        Data = $"Block #{blockHeader.Number.Value} mined"
//    };

//    await _transactionLogService.CreateNetworkEventAsync(networkEvent);
//    _logger.LogInformation("New block mined: #{BlockNumber}", blockHeader.Number.Value);
//}


// ==================== کامنت‌های جداگانه برای استفاده آینده ====================

/*
// برای گوش دادن به بلاک‌های جدید (BLOCK_MINED):
private async Task SubscribeToNewBlocks()
{
    var blockSubscription = new EthNewBlockHeadersObservableSubscription(_webSocketClient);

    _blockSubscription = blockSubscription.GetSubscriptionDataResponsesAsObservable()
        .Subscribe(async blockHeader =>
        {
            try
            {
                await ProcessNewBlockMined(blockHeader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing new block event");
            }
        },
        ex => _logger.LogError(ex, "Error in block subscription"),
        () => _logger.LogInformation("Block subscription completed"));

    await blockSubscription.SubscribeAsync();
}

private async Task ProcessNewBlockMined(Block blockHeader)
{
    var networkEvent = new NetworkEventLog
    {
        EventType = BlockchainEventType.BlockMined,
        BlockNumber = (long)blockHeader.Number.Value,
        Hash = blockHeader.BlockHash,
        TimeStamp = DateTime.UtcNow,
        Data = $"Block #{blockHeader.Number.Value} mined"
    };

    await _transactionLogService.CreateNetworkEventAsync(networkEvent);
    _logger.LogInformation("New block mined: #{BlockNumber}", blockHeader.Number.Value);
}
*/

/*
// برای مانیتورینگ وضعیت شبکه (NETWORK_STATUS):
private async Task MonitorNetworkStatus(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            var syncing = await _web3.Eth.Syncing.SendRequestAsync();
            await ProcessNetworkStatus(syncing);

            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
        catch (TaskCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking network status");
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }
}

private async Task ProcessNetworkStatus(SyncingOutput syncingResult)
{
    string status;
    if (syncingResult.IsSyncing)
    {
        status = $"Syncing - Current: {syncingResult.CurrentBlock}, Highest: {syncingResult.HighestBlock}";
    }
    else
    {
        status = "Fully synced";
    }

    var networkEvent = new NetworkEventLog
    {
        EventType = BlockchainEventType.NetworkStatus,
        TimeStamp = DateTime.UtcNow,
        Data = status
    };

    await _transactionLogService.CreateNetworkEventAsync(networkEvent);
    _logger.LogInformation("Network status: {Status}", status);
}
*/

/*
// فیلدهای اضافی برای subscriptionهای شبکه:
private IDisposable _blockSubscription;
private IDisposable _syncingSubscription;
*/

/*
// اضافه کردن به متد CleanupConnection:
_blockSubscription?.Dispose();
_syncingSubscription?.Dispose();
_blockSubscription = null;
_syncingSubscription = null;
*/

/*
// اضافه کردن به متد UnsubscribeNetworkEvents:
_blockSubscription?.Dispose();
_syncingSubscription?.Dispose();
_blockSubscription = null;
_syncingSubscription = null;
*/

/*
// اضافه کردن به متد SubscribeToNetworkLevelEvents:
await SubscribeToNewBlocks();
await MonitorNetworkStatus(cancellationToken);
*/