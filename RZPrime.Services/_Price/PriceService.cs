using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using RZPrime.Services._Inventory.DTOs.Storages;
using RZPrime.Services._Price.DTOs.Results;
using RZPrime.Services._Price.DTOs.Settings;
using RZPrime.Utilities.Exceptions.Common;
using System.Collections.Concurrent;
using System.Text.Json;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._Price
{
    public class PriceService(
        AvailableTokensSettings _availableTokenDatas,
        CallPriceSettings _callPriceSettings,
       ILogger<PriceService> logger,
        InventoryStorage _inventoryStorage) : IPriceService, ISingletonDependency
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly ILogger<PriceService> _logger = logger;
        private decimal _cachedBnbPrice = 300m;
        private DateTime _lastBnbPriceUpdate = DateTime.MinValue;



        public async Task<PriceResult> FetchTokenPriceAsync(string tokenName)
        {
            string url = $"{_callPriceSettings.BaseUrl}/tokens/symbol/{tokenName.ToUpper()}/price";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", _callPriceSettings.ApiKey); 
                request.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(jsonString);

                var status = doc.RootElement.GetProperty("status").GetString();
                if (status != "success")
                    return null;

                var data = doc.RootElement.GetProperty("data");

                decimal price = decimal.Parse(data.GetProperty("price").GetString()!);
                string tokenSymbol = data.GetProperty("symbol").GetString()!;

                return new PriceResult
                {
                    TokenName = tokenSymbol,
                    Price = price,
                    TokenNetwork = "BSC"
                };
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Error fetching price in RZ for {tokenName.ToUpper()}: {ex.Message}");
                return await FetchTokenPriceFromGeckoTerminalAsync(tokenName);
            }
        }

        public async Task FetchAllPricesAsync()
        {
            foreach (var token in _availableTokenDatas)
            {
                var priceData = await FetchTokenPriceAsync(token.Name);
                if (priceData != null)
                {
                    _inventoryStorage.UpdatePrice(token.Name, priceData);
                }

                await Task.Delay(5000);
            }
        }
       
        public async Task<Dictionary<string, PriceResult>> FetchAllPricesForInternalUsageAsync()
        {
            var result = new ConcurrentDictionary<string, PriceResult>();

            var tasks = _availableTokenDatas.Select(async token =>
            {
                var priceData = await FetchTokenPriceAsync(token.Name);
                if (priceData != null)
                {
                    result[token.Name] = priceData;
                }
            });

            await Task.WhenAll(tasks);

            return result.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public async Task<EffectivePriceResult> CalculateEffectivePriceAsync(string tokenName, decimal assetQuantity,decimal USDTAmount) 
        {
            // Validate input parameters
            if (assetQuantity <= 0) throw new BadRequestException("Token quantity must be greater than zero.", nameof(assetQuantity));
            var priceData = await FetchTokenPriceAsync(tokenName);


            if (priceData == null)
            {
                if (!_inventoryStorage.TryGetValue(tokenName.ToUpper(), out var value) || value == null || value.Price == null)
                {
                    throw new BadRequestException(nameof(priceData), "Price data cannot be null.");
                }

                priceData = value.Price;
            }

            try
            {
                var price = priceData.Price;
                var effectivePrice = USDTAmount / assetQuantity;
                var priceImpact = (effectivePrice - priceData.Price) / priceData.Price * 100;
                return new EffectivePriceResult
                {
                    EffectivePrice = effectivePrice,
                    Price = price ,
                    Impact = priceImpact
                };
                               
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calculating effective price for {Token}", priceData.TokenName);
                throw new BaseException("Failed to calculate effective price due to an unexpected error.");
            }
        }

        /// <summary>
        /// this method use for fetch price data with token name
        /// </summary>
        /// <param name="tokenName"></param>
        /// <param name="poolId"></param>
        /// <returns></returns>
        public async Task<PriceResult> FetchTokenPriceFromGeckoTerminalAsync(string tokenName, string poolId = null)
        {
            var pool = poolId == null ? _availableTokenDatas.FirstOrDefault(q => q.Name == tokenName.ToUpper()).PoolId : poolId;
            string url = $"https://api.geckoterminal.com/api/v2/networks/bsc/pools/{pool}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                //_logger.LogInformation(jsonString);
                using JsonDocument doc = JsonDocument.Parse(jsonString);

                // Go into data → attributes
                var attributes = doc.RootElement
                    .GetProperty("data")
                    .GetProperty("attributes");

                decimal basePrice = decimal.Parse(attributes.GetProperty("base_token_price_usd").GetString()!);
                decimal quotePrice = decimal.Parse(attributes.GetProperty("quote_token_price_usd").GetString()!);

                decimal liquidityUsd = decimal.Parse(attributes.GetProperty("reserve_in_usd").GetString()!);
                decimal volume24h = decimal.Parse(attributes.GetProperty("volume_usd").GetProperty("h24").GetString()!);
                decimal? poolFee = attributes.TryGetProperty("pool_fee_percentage", out var feeProp) && feeProp.ValueKind != JsonValueKind.Null
                                   ? decimal.Parse(feeProp.GetString()!)
                                   : (decimal?)null;

                return new PriceResult
                {
                    //TokenAddress = attributes.GetProperty("address").GetString(),
                    //TokenName = attributes.GetProperty("name").GetString(),
                    TokenName = tokenName,
                    TokenNetwork = "BSC",
                    Price = basePrice,
                    //LiquidityUsd = liquidityUsd,
                    //Volume24hUsd = volume24h,
                    //PoolFeeRate = poolFee,
                    //ReserveBaseUsd = basePrice * liquidityUsd,
                    //ReserveQuoteUsd = quotePrice * liquidityUsd,
                    //QuoteTokenPriceUsd = quotePrice
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching token price gecko for {poolId}: {ex.Message}");
                return null;
            }
        }

        #region Methods for use Gecko Terminal

        //public class PriceResult
        //{
        //    public string TokenAddress { get; set; }
        //    public string TokenName { get; set; }
        //    public string TokenNetwork { get; set; }
        //    public decimal Price { get; set; }
        //    public decimal LiquidityUsd { get; set; }
        //    public decimal Volume24hUsd { get; set; }
        //    public decimal? PoolFeeRate { get; set; }
        //    public decimal ReserveBaseUsd { get; set; }
        //    public decimal ReserveQuoteUsd { get; set; }
        //    public decimal QuoteTokenPriceUsd { get; set; }
        //}




        ///// <summary>
        ///// this method use for scheduler for fetching data
        ///// </summary>
        ///// <returns></returns>
        //public async Task FetchAllPricesFromGeckoTerminalAsync() 
        //{
        //    var tasks = _availableTokenDatas.Select(async token =>
        //    {
        //        var priceData = await FetchTokenPriceFromGeckoTerminalAsync(token.Name, token.PoolId);
        //        if (priceData != null)
        //        {
        //            _inventoryStorage.UpdatePrice(token.Name, priceData);
        //        }
        //    });

        //    await Task.WhenAll(tasks);
        //}

        //public async Task<Dictionary<string, PriceResult>> FetchAllPricesFromGeckoTerminalForInternalUsageAsync()
        //{
        //    var result = new ConcurrentDictionary<string, PriceResult>();

        //    var tasks = _availableTokenDatas.Select(async token =>
        //    {
        //        var priceData = await FetchTokenPriceFromGeckoTerminalAsync(token.Name, token.PoolId);
        //        if (priceData != null)
        //        {
        //            result[token.Name] = priceData;
        //        }
        //    });

        //    await Task.WhenAll(tasks);

        //    return result.ToDictionary(kv => kv.Key, kv => kv.Value);
        //}




        ///// <summary>
        ///// this method calculate the effective price by user given quantity for buying the token
        ///// </summary>
        ///// <param name="assetQuantity"></param>
        ///// <param name="priceData"></param>
        ///// <returns></returns>
        ///// <exception cref="BadRequestException"></exception>
        //public async Task<EffectivePriceResult> CalculateEffectivePriceAsync(string tokenName, decimal assetQuantity)
        //{
        //    // Validate input parameters
        //    if (assetQuantity <= 0) throw new BadRequestException("Token quantity must be greater than zero.", nameof(assetQuantity));
        //    var priceData = await FetchTokenPriceFromGeckoTerminalAsync(tokenName);


        //    if (priceData == null) throw new BadRequestException(nameof(priceData), "Price data cannot be null.");

        //    try
        //    {
        //        // Get BNB price (needed for calculations)
        //        var bnbPriceUsd = await GetBnbPriceUsdAsync();

        //        var totalReserveUsd = priceData.LiquidityUsd;

        //        var reserveToken = totalReserveUsd * 0.5m / priceData.Price;

        //        var reserveQuote = totalReserveUsd * 0.5m / priceData.QuoteTokenPriceUsd;



        //        if (reserveToken <= 0 || reserveQuote <= 0)
        //            throw new InvalidOperationException("Calculated reserves are invalid. Pool may be imbalanced.");

        //        var maxTradeSize = reserveToken * 0.01m;
        //        if (assetQuantity > maxTradeSize)
        //        {
        //            throw new InvalidOperationException($"Trade size too large. Maximum available: {maxTradeSize} tokens (1% of pool liquidity).");
        //        }


        //        var k = reserveToken * reserveQuote; // Constant product
        //        var newReserveToken = reserveToken - assetQuantity;
        //        var newReserveQuote = k / newReserveToken;
        //        var quoteTokensRequired = newReserveQuote - reserveQuote;

        //        var fee = priceData.PoolFeeRate ?? 0.0025m;
        //        var quoteTokensToPay = quoteTokensRequired / (1 - fee);

        //        var totalCostUsd = quoteTokensToPay * priceData.QuoteTokenPriceUsd;
        //        var effectivePricePerToken = totalCostUsd / assetQuantity;

        //        var priceImpact = (effectivePricePerToken - priceData.Price) / priceData.Price * 100;

        //        return new EffectivePriceResult
        //        {
        //            MarketPrice = priceData.Price,
        //            EffectivePrice = effectivePricePerToken,
        //            PriceImpact = priceImpact,
        //            TotalCost = totalCostUsd,
        //            BnbPriceUsd = bnbPriceUsd,
        //            TokenReserve = reserveToken,
        //            BnbReserve = reserveQuote // Note: This is actually the quote token reserve
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Unexpected error calculating effective price for {Token}", priceData.TokenName);
        //        throw new BaseException("Failed to calculate effective price due to an unexpected error.");
        //    }
        //}


        ///// <summary>
        ///// this method calculate the effective price by user given quantity for buying the token
        ///// </summary>
        ///// <param name="assetQuantity"></param>
        ///// <param name="priceData"></param>
        ///// <returns></returns>
        ///// <exception cref="BadRequestException"></exception>
        //public async Task<EffectivePriceResult> CalculateEffectivePriceForLandingAsync(string tokenName, decimal assetQuantity)
        //{
        //    if (assetQuantity <= 0) throw new BadRequestException("Token quantity must be greater than zero.", nameof(assetQuantity));

        //    PriceResult priceData;
        //    var priceDataExists = _inventoryStorage.TryGetValue(tokenName, out var res);
        //    if (res == null)
        //    {
        //        priceData = await FetchTokenPriceFromGeckoTerminalAsync(tokenName);
        //    }
        //    else
        //    {
        //        priceData = res.Price;
        //    }

        //    if (priceData == null) throw new BadRequestException(nameof(priceData), "Price data cannot be null.");

        //    try
        //    {
        //        // Get BNB price (needed for calculations)
        //        var bnbPriceUsd = await GetBnbPriceUsdAsync();

        //        // Calculate reserves in token units
        //        // From the Python code, we see reserve_in_usd is the total pool value
        //        // So we need to split it between base and quote tokens
        //        var totalReserveUsd = priceData.LiquidityUsd;

        //        // Calculate base token reserves (amount of INSURANCE in pool)
        //        var reserveToken = totalReserveUsd * 0.5m / priceData.Price;

        //        // Calculate quote token reserves (amount of MGC in pool)
        //        var reserveQuote = totalReserveUsd * 0.5m / priceData.QuoteTokenPriceUsd;

        //        // Validate reserves
        //        if (reserveToken <= 0 || reserveQuote <= 0)
        //            throw new InvalidOperationException("Calculated reserves are invalid. Pool may be imbalanced.");

        //        // More conservative trade size check (1% of pool)
        //        var maxTradeSize = reserveToken * 0.01m;
        //        if (assetQuantity > maxTradeSize)
        //        {
        //            throw new InvalidOperationException($"Trade size too large. Maximum available: {maxTradeSize} tokens (1% of pool liquidity).");
        //        }

        //        // Calculate required quote tokens using proper AMM formula
        //        // Following the Python implementation exactly
        //        var k = reserveToken * reserveQuote; // Constant product
        //        var newReserveToken = reserveToken - assetQuantity;
        //        var newReserveQuote = k / newReserveToken;
        //        var quoteTokensRequired = newReserveQuote - reserveQuote;

        //        // Apply fee (0.25% for PancakeSwap)
        //        var fee = priceData.PoolFeeRate ?? 0.0025m;
        //        var quoteTokensToPay = quoteTokensRequired / (1 - fee);

        //        // Convert to USD
        //        var totalCostUsd = quoteTokensToPay * priceData.QuoteTokenPriceUsd;
        //        var effectivePricePerToken = totalCostUsd / assetQuantity;

        //        // Calculate price impact percentage
        //        var priceImpact = (effectivePricePerToken - priceData.Price) / priceData.Price * 100;

        //        return new EffectivePriceResult
        //        {
        //            MarketPrice = priceData.Price,
        //            EffectivePrice = effectivePricePerToken,
        //            PriceImpact = priceImpact,
        //            TotalCost = totalCostUsd,
        //            BnbPriceUsd = bnbPriceUsd,
        //            TokenReserve = reserveToken,
        //            BnbReserve = reserveQuote 
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Unexpected error calculating effective price for {Token}", priceData.TokenName);
        //        throw new BaseException("Failed to calculate effective price due to an unexpected error.");
        //    }
        //}


        ///// <summary>
        ///// this method work with bnb cache price
        ///// </summary>
        ///// <returns></returns>
        //private async Task<decimal> GetBnbPriceUsdAsync()
        //{
        //    if ((DateTime.UtcNow - _lastBnbPriceUpdate).TotalMinutes < 5)
        //    {
        //        return _cachedBnbPrice;
        //    }

        //    try
        //    {
        //        var newPrice = await FetchBnbPriceFromApi();
        //        _cachedBnbPrice = newPrice;
        //        _lastBnbPriceUpdate = DateTime.UtcNow;
        //        return newPrice;
        //    }
        //    catch
        //    {
        //        return _cachedBnbPrice;
        //    }
        //}


        ///// <summary>
        ///// for fetching bnb price from api
        ///// </summary>
        ///// <returns></returns>
        //private async Task<decimal> FetchBnbPriceFromApi()
        //{
        //    try
        //    {

        //        var response = await _httpClient.GetAsync("https://api.coingecko.com/api/v3/simple/price?ids=binancecoin&vs_currencies=usd");
        //        response.EnsureSuccessStatusCode();

        //        var jsonString = await response.Content.ReadAsStringAsync();
        //        using var doc = JsonDocument.Parse(jsonString);

        //        return doc.RootElement
        //            .GetProperty("binancecoin")
        //            .GetProperty("usd")
        //            .GetDecimal();
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error fetching BNB price, using default value 300");
        //        return 300m; // Default fallback value
        //    }
        //}
        #endregion

    }

}
