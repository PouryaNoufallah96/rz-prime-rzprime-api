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
using RZPrime.DTOs.Contracts;
using RZPrime.Services._BlockChainWebSocket.DTOs;
using RZPrime.Services._Inventory;
using RZPrime.Services._Price.DTOs.Settings;
using RZPrime.Services._TransactionLog;
using RZPrime.Services._TransactionLog.DTOs;
using System.Numerics;
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
        private bool _isDisposed = false;
        private IDisposable _transactionSubscription;
        private IDisposable _contractEventsSubscription;
        private IDisposable _incomingTransferSubscription;
        private BigInteger _lastProcessedBlock = 0;
        //private CancellationTokenSource _ctsReconnect = new();
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
                    await ConnectAndSubscribe(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Blockchnain Service is stopping...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in blockchain main loop. Reconnecting in 5 seconds...");
                    CleanupConnection();
                    await Task.Delay(5000, stoppingToken);
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Subscription ended unexpectedly. Reconnecting in 5 seconds...");
                    CleanupConnection();
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }


        private async Task ConnectAndSubscribe(CancellationToken cancellationToken)
        {
            _webSocketClient = new StreamingWebSocketClient(_settings.WsUrl);

            try
            {
                await _webSocketClient.StartAsync();
                _logger.LogInformation("WebSocket client connected.");

                // Subscribe to contract events
                await SubscribeToContractEvents(cancellationToken);
                _logger.LogInformation("Contract events subscription active.");

                // Subscribe to transaction confirmations
                //await SubscribeToTransactionConfirmations();

                // Subscribe to incoming transfers
                await SubscribeToIncomingTransfers(cancellationToken);
                _logger.LogInformation("Incoming transfers subscription active.");


                _logger.LogInformation("All subscriptions active.");

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var blockNumber = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                        _logger.LogDebug("Heartbeat block: {BlockNumber}", blockNumber.Value);
                    }
                    catch (Exception)
                    {
                        _logger.LogWarning("Heartbeat failed. Reconnecting...");
                        break;
                    }

                    await Task.Delay(30000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting/subscribing. Will reconnect in 5s...");
                throw;
            }
            finally
            {
                CleanupConnection();              
            }
        }

       
        private void CleanupConnection()
        {
            try
            {
                _contractEventsSubscription?.Dispose();
                _transactionSubscription?.Dispose();
                _incomingTransferSubscription?.Dispose();
                _webSocketClient?.Dispose();

                _contractEventsSubscription = null;
                _transactionSubscription = null;
                _incomingTransferSubscription = null;
                _webSocketClient = null;

                _logger.LogInformation("Cleaned up previous connections/subscriptions.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup.");
            }
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
                        var orderRegistered = log.DecodeEvent<OrderRegisteredEventDTO>();
                        if (orderRegistered != null)
                        {
                            _logger.LogInformation("OrderRegistered: {OrderId} by {User}", orderRegistered.Event.OrderId, orderRegistered.Event.User);
                            //_lastProcessedBlock = log.BlockNumber.Value + 1;

                            //await LogOrderRegisteredEvent(orderRegistered, log);
                            return;
                        }

                        var orderExecuted = log.DecodeEvent<OrderExecutedEventDTO>();
                        if (orderExecuted != null)
                        {
                            _logger.LogInformation("OrderExecuted: {OrderId} by {User}", orderExecuted.Event.OrderId, orderExecuted.Event.User);
                            _lastProcessedBlock = log.BlockNumber.Value + 1;

                            await LogOrderExecutedEvent(orderExecuted, log);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        ForceReconnect("Error decoding blockchain event", ex);
                    }
                },
               ex => ForceReconnect("Contract events subscription error", ex),
               () => ForceReconnect("Contract events subscription completed unexpectedly"));

            var filter = new NewFilterInput
            {
                Address = new[] { _settings.ContractAddress },
                FromBlock = _lastProcessedBlock > 0
                    ? new BlockParameter(_lastProcessedBlock.ToHexBigInteger())
                    : BlockParameter.CreateLatest()
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
                        ForceReconnect("Error processing incoming token transfer", ex);
                    }

                },
                ex => ForceReconnect("Error in incoming transfer subscription", ex),
                () => ForceReconnect("Incoming transfer subscription completed unexpectedly"));

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

        private void ForceReconnect(string reason, Exception ex = null)
        {
            _logger.LogWarning(ex, "Force reconnect triggered. Reason: {Reason}", reason);
            CleanupConnection();
            throw new Exception("Force reconnect requested due to: " + reason, ex);
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


    }
}
