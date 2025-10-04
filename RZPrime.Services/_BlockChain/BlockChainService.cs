using Microsoft.Extensions.Logging;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20.TokenList;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using RZPrime.Domain.Collections;
using RZPrime.DTOs.Contracts;
using RZPrime.Services._BlockChain;
using RZPrime.Services._BlockChain.DTOs;
using RZPrime.Services._BlockChain.DTOs.Results;
using RZPrime.Services._BlockChain.DTOs.Settings;
using RZPrime.Services._BlockChainWebSocket.DTOs;
using RZPrime.Services._PancakeSwap;
using RZPrime.Services._Price.DTOs.Settings;
using RZPrime.Services._TransactionLog;
using RZPrime.Services._TransactionLog.DTOs;
using RZPrime.Utilities.Exceptions.Common;
using RZPrime.Utilities.Extension;
using System.Numerics;
using System.Reactive.Linq;
using System.Threading;
using static RZPrime.Utilities.Constants.RegisterMode;

public class BlockChainService : IBlockChainService, ISingletonDependency
{
    private const string ContractAbi = TokenForwardSaleAbi.Value;
    private const string ERC20Abi = TokenForwardSaleAbi.ERC20Abi;
    private readonly BlockChainSettings _settings;
    private readonly ILogger<BlockChainService> _logger;
    private readonly ITransactionLogService _transactionLogService;
    private readonly IMulticallService _multicallService;
    private readonly AvailableTokensSettings _availableTokenData;
    private readonly Web3 _web3;
    private readonly Account _account;
    private readonly Contract _contract;

    public BlockChainService(BlockChainSettings settings,
        ILogger<BlockChainService> logger,
        ITransactionLogService transactionLogService,
        IMulticallService multicallService,
        AvailableTokensSettings availableTokenData)
    {
        _settings = settings;
        _logger = logger;
        _transactionLogService = transactionLogService;
        _multicallService = multicallService;
        _availableTokenData = availableTokenData;
        if (string.IsNullOrEmpty(_settings.PrivateKey))
            throw new InvalidOperationException("Blockchain private key is not configured.");

        _account = new Account(_settings.PrivateKey, _settings.ChainId);
        _web3 = new Web3(_account, _settings.RpcUrl);
        _web3.TransactionManager.UseLegacyAsDefault = true;

        _contract = _web3.Eth.GetContract(ContractAbi, _settings.ContractAddress);
    }

    #region Transactional Methods
    public async Task<TransactionResult> RegisterOrderOnBlockChainAsync(Order order)
    {
        var tokenData = ValidateToken(order.TokenName);
        var tokenAmountInWei = ConvertToWei(order.TokenAmount, tokenData.PriceDecimalPlaces);
        var payAmountInWei = ConvertToWei(order.FinalAmount);
        var endTime = order.PayOffDate.ToUnixTimeSeconds();

        try
        {
            var validUserAddress = ValidateAndConvertToChecksumAddress(order.WalletAddress);
            var validBuyTokenAddress = ValidateAndConvertToChecksumAddress(order.TokenAddress);
            ValidateOrderParameters(validUserAddress, order.OrderId, tokenAmountInWei, payAmountInWei, endTime);

            //_logger.LogInformation("Registering order {OrderId} for user {UserAddress} on blockchain.", order.Id, validUserAddress);

            var registerFunction = _contract.GetFunction("registerUserOrder");

            var gasPrice = await GetOptimalGasPriceAsync();
            var gas = new HexBigInteger(_settings.GetDefaultGasLimit());

            var transactionReceipt = await registerFunction.SendTransactionAndWaitForReceiptAsync(
                from: _account.Address,
                gas: gas,
                gasPrice: new HexBigInteger(gasPrice),
                value: new HexBigInteger(0),
                functionInput: new object[] { validUserAddress, order.OrderId, validBuyTokenAddress, tokenAmountInWei, payAmountInWei, endTime }
            );

            if (transactionReceipt.Status.Value == 1)
            {
                _logger.LogInformation("Successfully registered order {OrderId}. TxHash: {TxHash}", order.OrderId, transactionReceipt.TransactionHash);
                await _transactionLogService.CreateOrderRegisteredTransactionLogAsync(new RegisteredTxLog
                {
                    OrderId = order.OrderId,
                    TokenAmount = order.TokenAmount,
                    TokenName = order.TokenName,
                    UserWallet = order.WalletAddress,
                    USDTAmount = order.FinalAmount,
                    RegisteredData =
                     new()
                     {
                         From = transactionReceipt.From,
                         To = transactionReceipt.To,
                         BlockNumber = (long)transactionReceipt.BlockNumber.Value,
                         Hash = transactionReceipt.TransactionHash,
                         EventType = BlockchainEventType.OrderRegistered,
                         Status = TransactionStatus.Pending,
                     }

                });
                return new TransactionResult
                {
                    Success = true,
                    TransactionHash = transactionReceipt.TransactionHash,
                    BlockNumber = transactionReceipt.BlockNumber.Value,
                    GasUsed = transactionReceipt.GasUsed.Value
                };

            }
            else
            {
                _logger.LogError("Registering order {OrderId} failed (reverted). TxHash: {TxHash}", order.OrderId, transactionReceipt.TransactionHash);
                return new TransactionResult { Success = false, TransactionHash = transactionReceipt.TransactionHash, ErrorMessage = "Transaction failed on blockchain (reverted)." };
            }
        }
        catch (SmartContractRevertException revertEx)
        {
            _logger.LogError(revertEx, "Contract logic error while registering order {OrderId}: {RevertMessage}", order.OrderId, revertEx.Message);
            return new TransactionResult { Success = false, ErrorMessage = $"Contract revert: {revertEx.Message}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while registering order {OrderId}.", order.OrderId);
            return new TransactionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<BigInteger> GetOptimalGasPriceAsync()
    {
        try
        {
            var currentGasPrice = await _web3.Eth.GasPrice.SendRequestAsync();

            var suggestedGasPrice = (BigInteger)((decimal)currentGasPrice.Value * 1.2m);

            var minGasPrice = UnitConversion.Convert.ToWei(_settings.GetMinGasPriceGwei(), UnitConversion.EthUnit.Gwei);
            var maxGasPrice = UnitConversion.Convert.ToWei(_settings.GetMaxGasPriceGwei(), UnitConversion.EthUnit.Gwei);

            var optimalPrice = BigInteger.Min(BigInteger.Max(suggestedGasPrice, minGasPrice), maxGasPrice);

            _logger.LogInformation($"Using gas price: {UnitConversion.Convert.FromWei(optimalPrice, UnitConversion.EthUnit.Gwei)} Gwei");
            return optimalPrice;
        }
        catch
        {
            var defaultPrice = UnitConversion.Convert.ToWei(_settings.GetDefaultGasPriceGwei(), UnitConversion.EthUnit.Gwei);
            _logger.LogWarning($"Using DEFAULT gas price: {_settings.GetDefaultGasPriceGwei()} Gwei");
            return defaultPrice;
        }
    }

    public async Task<TransactionResult> ExecuteOrderOnBlockchainAsync(string orderId)
    {
        try
        {
            var executeFunction = _contract.GetFunction("executeOrder");
            var gas = new HexBigInteger(_settings.GetDefaultGasLimit());

            var transactionReceipt = await executeFunction.SendTransactionAndWaitForReceiptAsync(
                from: _account.Address, gas: gas, value: new HexBigInteger(0),
                functionInput: new object[] { orderId }
            );

            return transactionReceipt.Status.Value == 1
                ? new TransactionResult { Success = true, TransactionHash = transactionReceipt.TransactionHash, BlockNumber = transactionReceipt.BlockNumber.Value, GasUsed = transactionReceipt.GasUsed.Value }
                : new TransactionResult { Success = false, TransactionHash = transactionReceipt.TransactionHash, ErrorMessage = "Transaction failed (reverted)." };
        }
        catch (SmartContractRevertException revertEx) { return new TransactionResult { Success = false, ErrorMessage = $"Contract revert: {revertEx.Message}" }; }
        catch (Exception ex) { return new TransactionResult { Success = false, ErrorMessage = ex.Message }; }
    }

    public async Task<OrderDetails?> GetOrderFromBlockchainAsync(string userAddress, string orderId)
    {
        try
        {
            var getOrderFunction = _contract.GetFunction("getOrder");
            var orderOutput = await getOrderFunction.CallDeserializingToObjectAsync<OrderOutputDto>(userAddress, orderId);

            if (orderOutput == null || orderOutput.BuyToken == "0x0000000000000000000000000000000000000000")
                return null;

            var endAtTimestamp = (long)orderOutput.EndAt;
            return new OrderDetails
            {
                BuyToken = orderOutput.BuyToken,
                TokenAmount = orderOutput.TokenAmount,
                PayAmount = orderOutput.PayAmount,
                EndAtTimestamp = endAtTimestamp,
                Status = (OrderStatus)orderOutput.Status,
                IsExpired = DateTimeOffset.UtcNow.ToUnixTimeSeconds() > endAtTimestamp,
                EndAtDateTimeUtc = DateTimeOffset.FromUnixTimeSeconds(endAtTimestamp).UtcDateTime,
                TokenAmountHumanReadable = ConvertFromWei(orderOutput.TokenAmount),
                PayAmountHumanReadable = ConvertFromWei(orderOutput.PayAmount)
            };
        }
        catch (SmartContractRevertException) { return null; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching order {OrderId}", orderId);
            throw;
        }
    }

    public async Task<string> SignDropOnBlockChainAsync(List<Order> ordersForDrop)
    {
        if (ordersForDrop == null || !ordersForDrop.Any())
            throw new ArgumentException("No orders provided for drop.");
        ordersForDrop.OrderBy(q => q.CreatedMoment);

        try
        {
            var userAddresses = ordersForDrop
                .Select(o => ValidateAndConvertToChecksumAddress(o.WalletAddress))
                .ToArray();

            var orderIds = ordersForDrop
                .Select(o => o.OrderId)
                .ToArray();

            var signatures = ordersForDrop
                .Select(o => o.DropSignature.HexToByteArray())
                .ToArray();

            var batchDropFunction = _contract.GetFunction("batchDropOrderBySig");

            var gasPrice = await GetOptimalGasPriceAsync();
            var gas = new HexBigInteger(_settings.GetDefaultGasLimit());

            var transactionReceipt = await batchDropFunction.SendTransactionAndWaitForReceiptAsync(
                from: _account.Address,
                gas: gas,
                gasPrice: new HexBigInteger(gasPrice),
                value: new HexBigInteger(0),
                functionInput: new object[] { userAddresses, orderIds, signatures }
            );

            if (transactionReceipt.Status.Value == 1)
            {
                _logger.LogInformation("Successfully executed batch drop. TxHash: {TxHash}", transactionReceipt.TransactionHash);
                return transactionReceipt.TransactionHash;
            }
            else
            {
                _logger.LogError("Batch drop failed (reverted). TxHash: {TxHash}", transactionReceipt.TransactionHash);
                return null;
            }
        }
        catch (SmartContractRevertException revertEx)
        {
            _logger.LogError(revertEx, "Contract logic error during batch drop: {Message}", revertEx.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during batch drop.");
            return null;
        }
    }

    public async Task PollMissingLogsAsync()
    {
        BigInteger _lastProcessedBlock = await _transactionLogService.GetLastCheckedBlockNumberAsync();

        BigInteger latestBlock = (BigInteger)(await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;

        if (_lastProcessedBlock >= latestBlock) return;

        const int blockChunk = 1000; 
        BigInteger fromBlock = _lastProcessedBlock > 0 ? _lastProcessedBlock : BigInteger.Zero;

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
                var logs = await _web3.Eth.Filters.GetLogs.SendRequestAsync(filter);

                foreach (var log in logs)
                {
                    var filterLog = log as FilterLog;
                    if (filterLog == null) continue;

                    _logger.LogInformation("Polled Log: Address={Address}, Topics={@Topics}, Data={Data}, Tx={TxHash}",
                        filterLog.Address, filterLog.Topics, filterLog.Data, filterLog.TransactionHash);

                    try
                    {
                        var orderExecuted = filterLog.DecodeEvent<OrderExecutedEventDTO>();
                        if (orderExecuted != null)
                        {
                            await LogOrderExecutedEvent(orderExecuted, filterLog);
                            _logger.LogInformation("Polled Log saved to DB: TxHash={TxHash} , OrderId:{OrderId}", filterLog.TransactionHash, orderExecuted.Event.OrderId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error decoding polled log");
                    }

                    _lastProcessedBlock = BigInteger.Max(_lastProcessedBlock, filterLog.BlockNumber.Value + 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling logs from {FromBlock} to {ToBlock}", fromBlock, toBlock);
            }

            fromBlock = toBlock + 1;
            await Task.Delay(5000);
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

    public async Task PrintTransactionLogsAsync(string txHash)
    {
        if (string.IsNullOrWhiteSpace(txHash))
        {
            Console.WriteLine("Transaction hash is empty!");
            return;
        }

        try
        {
            var receipt = await _web3.Eth.Transactions.GetTransactionReceipt
                .SendRequestAsync(txHash);

            if (receipt == null)
            {
                Console.WriteLine($"Transaction receipt not found for {txHash}");
                return;
            }

            Console.WriteLine($"Transaction Status: {(receipt.Status.Value == 1 ? "Success" : "Failed")}");
            Console.WriteLine($"Block Number: {receipt.BlockNumber.Value}");
            //Console.WriteLine($"Number of Logs: {receipt.Logs.Count}");

            foreach (var log in receipt.Logs)
            {
                var logObj = log as FilterLog;
                if (logObj != null)
                {
                    Console.WriteLine("------ Log ------");
                    Console.WriteLine($"Address: {logObj.Address}");
                    Console.WriteLine($"Topics: {string.Join(", ", logObj.Topics)}");
                    Console.WriteLine($"Data: {logObj.Data}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching transaction receipt: {ex.Message}");
        }
    }


    #endregion


    #region Balance methods

    public async Task<Dictionary<string, decimal>> GetBalancesMultiCallAsync()
    {
        var balances = new Dictionary<string, decimal>();
        var contractAddress = _settings.ContractAddress;
        var tokens = _availableTokenData;
        //var bnbBalance = await _web3.Eth.GetBalance.SendRequestAsync(contractAddress);
        //balances["BNB"] = UnitConversion.Convert.FromWei(bnbBalance);

        if (tokens == null || !tokens.Any())
            return balances;

        var calls = new List<MulticallCall>();
        var tokenList = tokens.ToList();

        foreach (var token in tokenList)
        {
            var erc20Contract = _web3.Eth.GetContract(ERC20Abi, token.Address);
            var balanceOfFunction = erc20Contract.GetFunction("balanceOf");
            calls.Add(new MulticallCall
            {
                Target = token.Address,
                CallData = balanceOfFunction.GetData(contractAddress).HexToByteArray()
            });
        }

        var returnDataList = await _multicallService.ExecuteCallsAsync(calls);

        var parameterDecoder = new ParameterDecoder();

        for (int i = 0; i < tokenList.Count; i++)
        {
            try
            {
                if (i < returnDataList.Count && returnDataList[i] != null && returnDataList[i].Length > 0)
                {
                    var parameters = parameterDecoder.DecodeDefaultData(
                        returnDataList[i],
                        new Parameter("uint256", "balance"));

                    var rawBalance = (BigInteger)parameters[0].Result;

                    balances[tokenList[i].Name] = UnitConversion.Convert.FromWei(
                        rawBalance, tokenList[i].PriceDecimalPlaces);
                }
                else
                {
                    balances[tokenList[i].Name] = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing token {tokenList[i].Name}: {ex.Message}");
                balances[tokenList[i].Name] = 0;
            }
        }

        return balances;
    }

    public async Task<Dictionary<string, decimal>> GetContractBalancesAsync()
    {
        var balances = new Dictionary<string, decimal>();

        try
        {
            //try
            //{
            //    var bnbBalance = await _web3.Eth.GetBalance.SendRequestAsync(_settings.ContractAddress);
            //    balances["BNB"] = UnitConversion.Convert.FromWei(bnbBalance);
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"Error getting BNB balance: {ex.Message}");
            //    balances["BNB"] = 0;
            //}

            foreach (var token in _availableTokenData)
            {
                try
                {
                    var erc20 = _web3.Eth.GetContract(ERC20Abi, token.Address);
                    var balanceOf = erc20.GetFunction("balanceOf");

                    var balance = await balanceOf.CallAsync<BigInteger>(_settings.ContractAddress);
                    balances[token.Name] = UnitConversion.Convert.FromWei(balance, token.PriceDecimalPlaces);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting balance for {token.Name}: {ex.Message}");
                    balances[token.Name] = 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error in GetContractBalancesAsync: {ex.Message}");

            if (!balances.ContainsKey("BNB"))
                balances["BNB"] = 0;
        }

        return balances;
    }

    public async Task<decimal> GetContractSingleBalanceAsync(string tokenName)
    {
        var tokenData = ValidateToken(tokenName);
        try
        {
            var erc20 = _web3.Eth.GetContract(ERC20Abi, tokenData.Address);
            var balanceOf = erc20.GetFunction("balanceOf");

            var balance = await balanceOf.CallAsync<BigInteger>(_settings.ContractAddress);
            var tokenBalance = UnitConversion.Convert.FromWei(balance, tokenData.PriceDecimalPlaces);
            return  tokenBalance;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting balance for {tokenData.Name}: {ex.Message}");
            return  0;
        }
    }


    #endregion





    public async Task<AccountBalanceDto> GetAccountBalanceAsync(string? address = null)
    {
        var targetAddress = address ?? _settings.PublicAddress;
        try
        {
            var balanceWei = await _web3.Eth.GetBalance.SendRequestAsync(targetAddress);
            return new AccountBalanceDto
            {
                Success = true,
                Address = targetAddress,
                BalanceInWei = balanceWei.Value,
                BalanceInEther = Web3.Convert.FromWei(balanceWei.Value)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get account balance for address {Address}", targetAddress);
            return new AccountBalanceDto { Success = false, Address = targetAddress, ErrorMessage = ex.Message };
        }
    }

    public async Task<NetworkStatusDto> GetNetworkStatusAsync()
    {
        try
        {
            var latestBlock = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());
            var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
            var isConnected = await _web3.Net.Listening.SendRequestAsync();

            return new NetworkStatusDto
            {
                Success = true,
                NetworkId = _account.ChainId.Value, // ChainId is known from account
                LatestBlockNumber = latestBlock.Number.Value,
                LatestBlockTimestamp = latestBlock.Timestamp.Value,
                GasPriceInWei = gasPrice.Value,
                IsConnected = isConnected
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get network status.");
            return new NetworkStatusDto { Success = false, IsConnected = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<List<BatchOrderResultDto>> BatchGetOrdersAsync(List<(string userAddress, string orderId)> userOrders)
    {
        var tasks = userOrders.Select(async order =>
        {
            try
            {
                var orderDetails = await GetOrderFromBlockchainAsync(order.userAddress, order.orderId);
                return new BatchOrderResultDto
                {
                    UserAddress = order.userAddress,
                    OrderId = order.orderId,
                    Success = orderDetails != null,
                    OrderDetails = orderDetails,
                    ErrorMessage = orderDetails == null ? "Order not found." : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch get order failed for OrderId {OrderId}", order.orderId);
                return new BatchOrderResultDto
                {
                    UserAddress = order.userAddress,
                    OrderId = order.orderId,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    public async Task<(OrderExecutedEventDTO Event, FilterLog log, TransactionReceipt Transaction)?> SyncExecutedOrderWithOrderIdAsync(
       string userAddress,
       string orderId,
       BigInteger fromBlock = default
       )
    {
        int batchSize = 5000;
        var executedEvent = _contract.GetEvent("OrderExecuted");


        var latestBlock = (await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;

        var currentFrom = fromBlock;

        while (currentFrom <= latestBlock)
        {
            var currentTo = currentFrom + batchSize;
            if (currentTo > latestBlock)
                currentTo = latestBlock;

            var filter = executedEvent.CreateFilterInput(
                new BlockParameter(currentFrom.ToHexBigInteger()),
                new BlockParameter(currentTo.ToHexBigInteger())
            );

            var logs = await executedEvent.GetAllChangesAsync<OrderExecutedEventDTO>(filter);

            foreach (var log in logs)
            {
                if (log.Event.User.Equals(userAddress, StringComparison.OrdinalIgnoreCase) &&
                    log.Event.OrderId == orderId)
                {
                    var tx = await _web3.Eth.Transactions
                        .GetTransactionReceipt
                        .SendRequestAsync(log.Log.TransactionHash);

                    return (log.Event, log.Log, tx);

                }
            }

            currentFrom = currentTo + 1;
        }

        return null;
    }


    public async Task<List<(OrderExecutedEventDTO Event, FilterLog log, TransactionReceipt Transaction)>> SyncExecutedOrdersDataWithOrderIdAsync(
        string userAddress,
        string orderId,
        BigInteger fromBlock = default
        )
    {
        int batchSize = 5000;
        var executedEvent = _contract.GetEvent("OrderExecuted");

        var results = new List<(OrderExecutedEventDTO, FilterLog, TransactionReceipt)>();

        var latestBlock = (await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync()).Value;

        var currentFrom = fromBlock;

        while (currentFrom <= latestBlock)
        {
            var currentTo = currentFrom + batchSize;
            if (currentTo > latestBlock)
                currentTo = latestBlock;

            var filter = executedEvent.CreateFilterInput(
                new BlockParameter(currentFrom.ToHexBigInteger()),
                new BlockParameter(currentTo.ToHexBigInteger())
            );

            var logs = await executedEvent.GetAllChangesAsync<OrderExecutedEventDTO>(filter);

            foreach (var log in logs)
            {
                if (log.Event.User.Equals(userAddress, StringComparison.OrdinalIgnoreCase) &&
                    log.Event.OrderId == orderId)
                {
                    var tx = await _web3.Eth.Transactions
                        .GetTransactionReceipt
                        .SendRequestAsync(log.Log.TransactionHash);

                    results.Add((log.Event, log.Log, tx));

                    return results;
                }
            }

            currentFrom = currentTo + 1;
        }

        return results;
    }



    #region Utility Methods (Unchanged)
    public BigInteger ConvertToWei(decimal amount, int decimals = 18)
    {
        if (amount < 0) throw new ArgumentException("Amount must be a positive number.");
        var factor = BigInteger.Pow(10, decimals);
        return (BigInteger)(amount * (decimal)factor);
    }

    public decimal ConvertFromWei(BigInteger weiAmount, int decimals = 18)
    {
        if (weiAmount < 0) throw new ArgumentException("Amount must be a positive integer.");
        var factor = (decimal)BigInteger.Pow(10, decimals);
        return (decimal)weiAmount / factor;
    }

    private string ValidateAndConvertToChecksumAddress(string address)
    {
        var addressUtil = new AddressUtil();
        if (!addressUtil.IsValidAddressLength(address) || !addressUtil.IsChecksumAddress(address) && !address.ToLower().Equals(address))
        {
            if (!addressUtil.IsValidEthereumAddressHexFormat(address))
            {
                throw new BadRequestException($"Invalid address format: {address}");
            }
            return addressUtil.ConvertToChecksumAddress(address);
        }
        return address;
    }

    private void ValidateOrderParameters(string userAddress, string orderId, BigInteger tokenAmount, BigInteger payAmount, BigInteger endTimestamp)
    {

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new BadRequestException("Order ID must be a non-empty string.", nameof(orderId));
        }

        if (tokenAmount <= 0 || payAmount <= 0)
        {
            throw new BadRequestException("Token amount and pay amount must be positive.");
        }

        var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (endTimestamp <= currentTimestamp)
        {
            throw new BadRequestException("End timestamp must be in the future.");
        }
    }

    private AvailableTokenData ValidateToken(string tokenName)
    {
        var tokenData = _availableTokenData.FirstOrDefault(q => q.Name.Equals(tokenName, StringComparison.CurrentCultureIgnoreCase))
            ?? throw new BadRequestException($"Unsupported token name! {tokenName}");
        return tokenData;
    }
    #endregion
}

//public async Task<TransactionResult> RegisterOrderOnBlockChainAsync(Order order)
//{
//    var tokenAmountInWei = ConvertToWei(order.Quantity);
//    var payAmountInWei = order.RegisteredAmount;
//    var endTime = order.PayOffDate.ToUnixTimeSeconds();

//    try
//    {
//        var validUserAddress = ValidateAndConvertToChecksumAddress(order.WalletAddress);
//        var validBuyTokenAddress = ValidateAndConvertToChecksumAddress(order.TokenAddress);
//        ValidateOrderParameters(validUserAddress, order.Id, tokenAmountInWei, payAmountInWei, endTime);

//        _logger.LogInformation("Registering order {OrderId} for user {UserAddress} on blockchain.", order.Id, validUserAddress);

//        var registerFunction = _contract.GetFunction("registerUserOrder");

//        var gas = new HexBigInteger(_settings.DefaultGasLimit);

//        var transactionReceipt = await registerFunction.SendTransactionAndWaitForReceiptAsync(
//            from: _account.Address,
//            gas: gas,
//            value: new HexBigInteger(0),
//            functionInput: new object[] { validUserAddress, order.Id, validBuyTokenAddress, tokenAmountInWei, payAmountInWei, endTime }
//        );

//        if (transactionReceipt.Status.Value == 1)
//        {
//            _logger.LogInformation("Successfully registered order {OrderId}. TxHash: {TxHash}", order.Id, transactionReceipt.TransactionHash);
//            return new TransactionResult
//            {
//                Success = true,
//                TransactionHash = transactionReceipt.TransactionHash,
//                BlockNumber = transactionReceipt.BlockNumber.Value,
//                GasUsed = transactionReceipt.GasUsed.Value
//            };
//        }
//        else
//        {
//            _logger.LogError("Registering order {OrderId} failed (reverted). TxHash: {TxHash}", order.Id, transactionReceipt.TransactionHash);
//            return new TransactionResult { Success = false, TransactionHash = transactionReceipt.TransactionHash, ErrorMessage = "Transaction failed on blockchain (reverted)." };
//        }
//    }
//    catch (SmartContractRevertException revertEx)
//    {
//        _logger.LogError(revertEx, "Contract logic error while registering order {OrderId}: {RevertMessage}", order.Id, revertEx.Message);
//        return new TransactionResult { Success = false, ErrorMessage = $"Contract revert: {revertEx.Message}" };
//    }
//    catch (Exception ex)
//    {
//        _logger.LogError(ex, "Unexpected error while registering order {OrderId}.", order.Id);
//        return new TransactionResult { Success = false, ErrorMessage = ex.Message };
//    }
//}


//public async Task<TransactionResult> RegisterOrderOnBlockChainAsync(Order order)
//{
//    var tokenAmountInWei = ConvertToWei(order.Quantity);
//    var payAmountInWei = order.RegisteredAmount;
//    var endTime = order.PayOffDate.ToUnixTimeSeconds();

//    try
//    {
//        var validUserAddress = ValidateAndConvertToChecksumAddress(order.WalletAddress);
//        var validBuyTokenAddress = ValidateAndConvertToChecksumAddress(order.TokenAddress);
//        ValidateOrderParameters(validUserAddress, order.Id, tokenAmountInWei, payAmountInWei, endTime);

//        _logger.LogInformation("Registering order {OrderId} for user {UserAddress} on blockchain.", order.Id, validUserAddress);

//        var registerFunction = _contract.GetFunction("registerUserOrder");

//        // Estimate gas price with buffer
//        var gasPrice = await EstimateOptimalGasPriceAsync();
//        var nonce = await _web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(_account.Address);

//        // Build transaction input
//        var transactionInput = registerFunction.CreateTransactionInput(
//            from: _account.Address,
//            gas: new HexBigInteger(_settings.DefaultGasLimit),
//            gasPrice: new HexBigInteger(gasPrice),
//            value: new HexBigInteger(0),
//            nonce: nonce,
//            functionInput: new object[] { validUserAddress, order.Id, validBuyTokenAddress, tokenAmountInWei, payAmountInWei, endTime }
//        );

//        // Estimate gas with buffer
//        try
//        {
//            var estimatedGas = await registerFunction.EstimateGasAsync(
//                from: _account.Address,
//                gas: new HexBigInteger(_settings.DefaultGasLimit),
//                value: new HexBigInteger(0),
//                functionInput: new object[] { validUserAddress, order.Id, validBuyTokenAddress, tokenAmountInWei, payAmountInWei, endTime }
//            );

//            var bufferedGas = (BigInteger)(estimatedGas.Value * 1.2m); // 20% buffer
//            transactionInput.Gas = new HexBigInteger(bufferedGas);

//            _logger.LogInformation($"Estimated gas: {estimatedGas}, using: {bufferedGas}");
//        }
//        catch (Exception ex)
//        {
//            _logger.LogWarning(ex, "Gas estimation failed, using default gas limit");
//        }

//        // Sign and send transaction
//        var signedTransaction = await _web3.Eth.TransactionManager.SignTransactionAsync(transactionInput);
//        var txHash = await _web3.Eth.Transactions.SendRawTransaction.SendRequestAsync(signedTransaction);

//        _logger.LogInformation($"Transaction sent: {txHash}");

//        // Wait for receipt
//        var transactionReceipt = await WaitForTransactionReceiptAsync(txHash);

//        if (transactionReceipt.Status.Value == 1)
//        {
//            _logger.LogInformation("Successfully registered order {OrderId}. TxHash: {TxHash}, Gas used: {GasUsed}",
//                order.Id, transactionReceipt.TransactionHash, transactionReceipt.GasUsed);

//            return new TransactionResult
//            {
//                Success = true,
//                TransactionHash = transactionReceipt.TransactionHash,
//                BlockNumber = transactionReceipt.BlockNumber.Value,
//                GasUsed = transactionReceipt.GasUsed.Value
//            };
//        }
//        else
//        {
//            _logger.LogError("Registering order {OrderId} failed (reverted). TxHash: {TxHash}",
//                order.Id, transactionReceipt.TransactionHash);

//            return new TransactionResult
//            {
//                Success = false,
//                TransactionHash = transactionReceipt.TransactionHash,
//                ErrorMessage = "Transaction failed on blockchain (reverted)."
//            };
//        }
//    }
//    catch (SmartContractRevertException revertEx)
//    {
//        _logger.LogError(revertEx, "Contract logic error while registering order {OrderId}: {RevertMessage}",
//            order.Id, revertEx.Message);

//        return new TransactionResult
//        {
//            Success = false,
//            ErrorMessage = $"Contract revert: {revertEx.Message}"
//        };
//    }
//    catch (Exception ex)
//    {
//        _logger.LogError(ex, "Unexpected error while registering order {OrderId}.", order.Id);
//        return new TransactionResult
//        {
//            Success = false,
//            ErrorMessage = ex.Message
//        };
//    }
//}

//private async Task<BigInteger> EstimateOptimalGasPriceAsync()
//{
//    try
//    {
//        // Get current gas price from network
//        var currentGasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
//        var suggestedGasPrice = (BigInteger)(currentGasPrice.Value * 1.1m); // 10% above current

//        // Cap at maximum/minimum
//        var maxGasPrice = UnitConversion.Convert.ToWei(_settings.MaxGasPriceGwei, UnitConversion.EthUnit.Gwei);
//        var minGasPrice = UnitConversion.Convert.ToWei(_settings.GasPriceGwei, UnitConversion.EthUnit.Gwei);

//        var optimalGasPrice = BigInteger.Min(BigInteger.Max(suggestedGasPrice, minGasPrice), maxGasPrice);

//        _logger.LogInformation($"Gas price: {UnitConversion.Convert.FromWei(optimalGasPrice, UnitConversion.EthUnit.Gwei)} gwei");
//        return optimalGasPrice;
//    }
//    catch (Exception ex)
//    {
//        _logger.LogWarning(ex, "Failed to estimate gas price, using default");
//        return UnitConversion.Convert.ToWei(_settings.GasPriceGwei, UnitConversion.EthUnit.Gwei);
//    }
//}

//private async Task<TransactionReceipt> WaitForTransactionReceiptAsync(string transactionHash)
//{
//    var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);

//    if (receipt != null)
//        return receipt;

//    // Poll for receipt if not immediately available
//    var pollingInterval = TimeSpan.FromSeconds(2);
//    var timeout = TimeSpan.FromMinutes(2);
//    var stopwatch = Stopwatch.StartNew();

//    while (receipt == null && stopwatch.Elapsed < timeout)
//    {
//        await Task.Delay(pollingInterval);
//        receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);
//    }

//    if (receipt == null)
//        throw new Exception("Transaction receipt not received within timeout period");

//    return receipt;
//}
//public async Task<List<(OrderExecutedEventDTO Event, FilterLog log, TransactionReceipt Transaction)>> test(
//string userAddress,
//string orderId,
//BigInteger fromBlock = default,
//BigInteger? toBlock = null)
//{
//    var executedEvent = _contract.GetEvent("OrderExecuted");
//    var filterAll = executedEvent.CreateFilterInput(
//        fromBlock: new BlockParameter(fromBlock.ToHexBigInteger()),
//        toBlock: toBlock.HasValue ? new BlockParameter(toBlock.Value.ToHexBigInteger()) : null);

//    var logs = await executedEvent.GetAllChangesAsync<OrderExecutedEventDTO>(filterAll);

//    var result = new List<(OrderExecutedEventDTO, FilterLog log, TransactionReceipt)>();

//    foreach (var log in logs)
//    {
//        if (log.Event.User.Equals(userAddress, StringComparison.OrdinalIgnoreCase) &&
//            log.Event.OrderId == orderId)
//        {
//            var tx = await _web3.Eth.Transactions
//                .GetTransactionReceipt
//                .SendRequestAsync(log.Log.TransactionHash);

//            result.Add((log.Event,log.Log, tx));
//        }
//    }

//    return result;
//}

//public async Task<List<(OrderExecutedEventDTO Event, FilterLog Log)>> SyncExecutedOrderFilterLogWithOrderIdAsync(
//   string userAddress,
//   string orderId,
//   BigInteger fromBlock = default, 
//   BigInteger? toBlock = null) 
//{
//    var executedEvent = _contract.GetEvent("OrderExecuted");
//    var filterAll = executedEvent.CreateFilterInput(
//     fromBlock: new BlockParameter(fromBlock.ToHexBigInteger()),
//     toBlock: toBlock.HasValue ? new BlockParameter(toBlock.Value.ToHexBigInteger()) : null);

//    var logs = await executedEvent.GetAllChangesAsync<OrderExecutedEventDTO>(filterAll);

//    return logs
//        .Where(x =>
//            x.Event.User.Equals(userAddress, StringComparison.OrdinalIgnoreCase) &&
//            x.Event.OrderId == orderId)
//        .Select(x => (x.Event, x.Log))
//        .ToList();
//}
