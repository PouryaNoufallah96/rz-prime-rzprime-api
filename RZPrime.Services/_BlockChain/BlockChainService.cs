using Microsoft.Extensions.Logging;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using RZPrime.Domain.Collections;
using RZPrime.DTOs.Contracts;
using RZPrime.Services._BlockChain;
using RZPrime.Services._BlockChain.DTOs;
using RZPrime.Services._BlockChain.DTOs.Results;
using RZPrime.Services._BlockChain.DTOs.Settings;
using RZPrime.Services._PancakeSwap;
using RZPrime.Services._Price.DTOs.Settings;
using RZPrime.Services._TransactionLog;
using RZPrime.Utilities.Exceptions.Common;
using RZPrime.Utilities.Extension;
using System.Numerics;
using System.Reactive.Linq;
using System.Text;
using static RZPrime.Utilities.Constants.RegisterMode;

public class BlockChainService : IBlockChainService, ISingletonDependency
{
    private const string ContractAbi = TokenForwardSaleAbi.Value;
    private const string ERC20Abi = TokenForwardSaleAbi.ERC20Abi;
    private readonly BlockChainSettings _settings;
    private readonly ILogger<BlockChainService> _logger;
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

    #endregion



    #region Campaign Methods

    public async Task<TransactionResult> CreateCampaignAsync(CreateCampaignOnBlockChainRequest request)
    {
        try
        {
            var campaignId = HexToByteArray32(request.CampaignReference);
            var startAt = new DateTimeOffset(request.FromOrderRegisterTime, TimeSpan.Zero).ToUnixTimeSeconds();
            var endAt = new DateTimeOffset(request.ToOrderRegisterTime, TimeSpan.Zero).ToUnixTimeSeconds();
            var maxUsers = new BigInteger(request.MaxUsers);
            var minUsdValue = ConvertToWei(request.MinUSDValue);
            var maxUsdValue = ConvertToWei(request.MaxUSDValue);
            var discountBps = ConvertPercentageToBps(request.DiscountPercentage);

            var function = _contract.GetFunction("createCampaign");
            var gasPrice = await GetOptimalGasPriceAsync();
            var gas = new HexBigInteger(_settings.GetDefaultGasLimit());

            var receipt = await function.SendTransactionAndWaitForReceiptAsync(
                from: _account.Address,
                gas: gas,
                gasPrice: new HexBigInteger(gasPrice),
                value: new HexBigInteger(0),
                functionInput: new object[] { campaignId, (BigInteger)startAt, (BigInteger)endAt, maxUsers, minUsdValue, maxUsdValue, discountBps, request.FirstOrder }
            );

            if (receipt.Status.Value == 1)
            {
                _logger.LogInformation("Campaign {CampaignRef} created. TxHash: {TxHash}", request.CampaignReference, receipt.TransactionHash);
                return new TransactionResult { Success = true, TransactionHash = receipt.TransactionHash };
            }

            _logger.LogError("Campaign {CampaignRef} creation failed (reverted). TxHash: {TxHash}", request.CampaignReference, receipt.TransactionHash);
            return new TransactionResult { Success = false, TransactionHash = receipt.TransactionHash, ErrorMessage = "Transaction failed on blockchain (reverted)." };
        }
        catch (SmartContractRevertException revertEx)
        {
            _logger.LogError(revertEx, "Contract revert while creating campaign {CampaignRef}: {Message}", request.CampaignReference, revertEx.Message);
            return new TransactionResult { Success = false, ErrorMessage = $"Contract revert: {revertEx.Message}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating campaign {CampaignRef}", request.CampaignReference);
            return new TransactionResult { Success = false, ErrorMessage = ex.Message };
        }
    }
   
    public async Task<TransactionResult> RemoveCampaignAsync(string campaignReference)
    {
        try
        {
            var campaignId = HexToByteArray32(campaignReference);

            var function = _contract.GetFunction("removeCampaign");
            var gasPrice = await GetOptimalGasPriceAsync();
            var gas = new HexBigInteger(_settings.GetDefaultGasLimit());

            var receipt = await function.SendTransactionAndWaitForReceiptAsync(
                from: _account.Address,
                gas: gas,
                gasPrice: new HexBigInteger(gasPrice),
                value: new HexBigInteger(0),
                functionInput: new object[] { campaignId }
            );

            if (receipt.Status.Value == 1)
            {
                _logger.LogInformation("Campaign {CampaignRef} removed. TxHash: {TxHash}", campaignReference, receipt.TransactionHash);
                return new TransactionResult { Success = true, TransactionHash = receipt.TransactionHash };
            }

            _logger.LogError("Campaign {CampaignRef} removal failed (reverted). TxHash: {TxHash}", campaignReference, receipt.TransactionHash);
            return new TransactionResult { Success = false, TransactionHash = receipt.TransactionHash, ErrorMessage = "Transaction failed on blockchain (reverted)." };
        }
        catch (SmartContractRevertException revertEx)
        {
            _logger.LogError(revertEx, "Contract revert while removing campaign {CampaignRef}: {Message}", campaignReference, revertEx.Message);
            return new TransactionResult { Success = false, ErrorMessage = $"Contract revert: {revertEx.Message}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while removing campaign {CampaignRef}", campaignReference);
            return new TransactionResult { Success = false, ErrorMessage = ex.Message };
        }
    }
    
    public async Task<TransactionResult> EditCampaignAsync(CreateCampaignOnBlockChainRequest request)
    {
        try
        {
            var campaignId = HexToByteArray32(request.CampaignReference);
            var startAt = new DateTimeOffset(request.FromOrderRegisterTime, TimeSpan.Zero).ToUnixTimeSeconds();
            var endAt = new DateTimeOffset(request.ToOrderRegisterTime, TimeSpan.Zero).ToUnixTimeSeconds();
            var maxUsers = new BigInteger(request.MaxUsers);
            var minUsdValue = ConvertToWei(request.MinUSDValue);
            var maxUsdValue = ConvertToWei(request.MaxUSDValue);
            var discountBps = ConvertPercentageToBps(request.DiscountPercentage);

            var function = _contract.GetFunction("editCampaign");
            var gasPrice = await GetOptimalGasPriceAsync();
            var gas = new HexBigInteger(_settings.GetDefaultGasLimit());

            var receipt = await function.SendTransactionAndWaitForReceiptAsync(
                from: _account.Address,
                gas: gas,
                gasPrice: new HexBigInteger(gasPrice),
                value: new HexBigInteger(0),
                functionInput: new object[] { campaignId, (BigInteger)startAt, (BigInteger)endAt, maxUsers, minUsdValue, maxUsdValue, discountBps, request.FirstOrder }
            );

            if (receipt.Status.Value == 1)
            {
                _logger.LogInformation("Campaign {CampaignRef} edited. TxHash: {TxHash}", request.CampaignReference, receipt.TransactionHash);
                return new TransactionResult { Success = true, TransactionHash = receipt.TransactionHash, BlockNumber = receipt.BlockNumber.Value, GasUsed = receipt.GasUsed.Value };
            }

            _logger.LogError("Campaign {CampaignRef} edit failed (reverted). TxHash: {TxHash}", request.CampaignReference, receipt.TransactionHash);
            return new TransactionResult { Success = false, TransactionHash = receipt.TransactionHash, ErrorMessage = "Transaction failed on blockchain (reverted)." };
        }
        catch (SmartContractRevertException revertEx)
        {
            _logger.LogError(revertEx, "Contract revert while editing campaign {CampaignRef}: {Message}", request.CampaignReference, revertEx.Message);
            return new TransactionResult { Success = false, ErrorMessage = $"Contract revert: {revertEx.Message}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while editing campaign {CampaignRef}", request.CampaignReference);
            return new TransactionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<TransactionResult> SetDiscountForAsync(string walletAddress, decimal discountPercentage)
    {
        try
        {
            var validAddress = ValidateAndConvertToChecksumAddress(walletAddress);
            var discountBps = ConvertPercentageToBps(discountPercentage);

            var function = _contract.GetFunction("setDiscountFor");
            var gasPrice = await GetOptimalGasPriceAsync();
            var gas = new HexBigInteger(_settings.GetDefaultGasLimit());

            var receipt = await function.SendTransactionAndWaitForReceiptAsync(
                from: _account.Address,
                gas: gas,
                gasPrice: new HexBigInteger(gasPrice),
                value: new HexBigInteger(0),
                functionInput: new object[] { validAddress, discountBps }
            );

            if (receipt.Status.Value == 1)
            {
                _logger.LogInformation("Discount set for wallet {Wallet}: {Discount}%. TxHash: {TxHash}", walletAddress, discountPercentage, receipt.TransactionHash);
                return new TransactionResult { Success = true, TransactionHash = receipt.TransactionHash, BlockNumber = receipt.BlockNumber.Value, GasUsed = receipt.GasUsed.Value };
            }

            _logger.LogError("SetDiscountFor wallet {Wallet} failed (reverted). TxHash: {TxHash}", walletAddress, receipt.TransactionHash);
            return new TransactionResult { Success = false, TransactionHash = receipt.TransactionHash, ErrorMessage = "Transaction failed on blockchain (reverted)." };
        }
        catch (SmartContractRevertException revertEx)
        {
            _logger.LogError(revertEx, "Contract revert while setting discount for {Wallet}: {Message}", walletAddress, revertEx.Message);
            return new TransactionResult { Success = false, ErrorMessage = $"Contract revert: {revertEx.Message}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while setting discount for {Wallet}", walletAddress);
            return new TransactionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<BigInteger> PreviewPaymentAmountAsync(string walletAddress, string orderId)
    {
        try
        {
            //var validAddress = ValidateAndConvertToChecksumAddress(walletAddress);

            var function = _contract.GetFunction("previewPaymentAmount");
            var rzusdAmountWei = await function.CallAsync<BigInteger>(walletAddress, orderId);

            //return ConvertFromWei(rzusdAmountWei);
            return rzusdAmountWei; 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing payment amount for wallet {Wallet}, orderId {OrderId}", walletAddress, orderId);
            throw;
        }
    }

    #endregion



    #region Campaign Utility Methods



    public static byte[] HexToByteArray32(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            throw new ArgumentException("Hex string is null or empty");

        var bytes = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(hex);

        if (bytes.Length > 32)
            throw new ArgumentException("Hex string is too long for bytes32");

        var padded = new byte[32];
        Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);

        return padded;
    }


    /// <summary>
    /// Converts a percentage (e.g. 5.5 for 5.5%) to basis points (e.g. 550).
    /// </summary>
    private static BigInteger ConvertPercentageToBps(decimal percentage)
    {
        return new BigInteger((int)(percentage * 100));
    }

    #endregion



    #region Balance methods

    public async Task<Dictionary<string, decimal>> GetBalancesMultiCallAsync()
    {
        var balances = new Dictionary<string, decimal>();
        var contractAddress = _settings.ContractAddress;
        var tokens = _availableTokenData.Where(q => q.SyncPrice);
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
            return tokenBalance;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting balance for {tokenData.Name}: {ex.Message}");
            return 0;
        }
    }

    #endregion





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
