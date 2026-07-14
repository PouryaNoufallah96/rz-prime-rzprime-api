using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using RZPrime.Services._Inventory.DTOs.Storages;
using RZPrime.Services._Price.DTOs.Results;
using RZPrime.Services._Price.DTOs.Settings;
using RZPrime.Services._Price.DTOs.Storages;
using RZPrime.Utilities.Exceptions.Common;
using Services._Price._RZPriceService;
using System.Collections.Concurrent;
using System.Text.Json;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Services._Price
{
    public class PriceService(
        AvailableTokensSettings _availableTokenDatas,
        CallPriceSettings _priceSetting,
        ILogger<PriceService> logger,
        RZUSDPriceStorage _rZUSDPriceStorage,
        InventoryStorage _inventoryStorage,
        IRZPriceService _rZPriceService) : IPriceService, ISingletonDependency
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly ILogger<PriceService> _logger = logger;
        private readonly SemaphoreSlim _priceLock = new(1, 1);



        /// <summary>
        /// use for inventory
        /// </summary>
        /// <returns></returns>
        public async Task<Dictionary<string, PriceResult>> FetchAllPricesForInternalUsageAsync()
        {
            var result = new ConcurrentDictionary<string, PriceResult>();
            var tokensForFetch = _availableTokenDatas.ToList();
            var tokenNames = tokensForFetch.Select(t => t.Name).ToList();

            try
            {
                var rzPriceResults = await _rZPriceService.FetchTokensPriceAsync(tokenNames);

                if (rzPriceResults != null && rzPriceResults.Any())
                {
                    foreach (var rzPrice in rzPriceResults)
                    {
                        var priceResult = new PriceResult
                        {
                            TokenName = rzPrice.TokenName,
                            TokenNetwork = rzPrice.TokenNetwork,
                            Price = rzPrice.Price
                        };
                        result.TryAdd(rzPrice.TokenName, priceResult);
                    }
                    if(tokenNames.Count != result.Count)
                    {
                        throw new Exception("Mismatch in fetched prices count");
                    }


                    return result.ToDictionary(kv => kv.Key, kv => kv.Value);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var coinMarketCapPrices = await SyncAllPricesFromCoinMarketCapAsync(tokensForFetch);

                    if (coinMarketCapPrices != null && coinMarketCapPrices.Any())
                    {
                        foreach (var price in coinMarketCapPrices)
                        {
                            result.TryAdd(price.TokenName, price);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error fetching all prices from CoinMarketCap fallback");
                }
            }

            return result.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public async Task FetchAllPricesAsync()
        {
            try
            {
                await FetchRZUSDPriceAsync();
                _logger.LogInformation("Successfully fetched RZUSD price");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching RZUSD price");
            }

            var tokensDataToSync = _availableTokenDatas
                .Where(t => t.SyncPrice)
                .ToList();

            var tokensNameToSync = tokensDataToSync
                .Select(t => t.Name)
                .ToList();

            if (!tokensNameToSync.Any())
            {
                _logger.LogInformation("No tokens to sync");
                return;
            }


            var fetchedPrices = new Dictionary<string, PriceResult>();

            try
            {
                var rzPriceResults = await _rZPriceService.FetchTokensPriceAsync(tokensNameToSync);

                if (rzPriceResults != null && rzPriceResults.Any())
                {
                    foreach (var rzPrice in rzPriceResults)
                    {
                        var priceResult = new PriceResult
                        {
                            TokenName = rzPrice.TokenName,
                            TokenNetwork = rzPrice.TokenNetwork,
                            Price = rzPrice.Price
                        };
                        fetchedPrices[rzPrice.TokenName] = priceResult;
                    }
                }
                if (tokensDataToSync.Count != fetchedPrices.Count)
                {
                    throw new Exception("Mismatch in fetched prices count");
                }
            }
            catch (Exception e)
            {
                var coinMarketCapPrices = await SyncAllPricesFromCoinMarketCapAsync(tokensDataToSync);

                if (coinMarketCapPrices != null && coinMarketCapPrices.Any())
                {
                    foreach (var price in coinMarketCapPrices)
                    {
                        fetchedPrices[price.TokenName] = price;
                    }

                    _logger.LogInformation("Successfully fetched {Count} prices from CoinMarketCap", coinMarketCapPrices.Count);
                }
            }

            foreach (var price in fetchedPrices.Values)
            {
                try
                {
                    _inventoryStorage.UpdatePrice(price.TokenName, price);
                    _logger.LogDebug("Updated price for token {Token}: {Price}", price.TokenName, price.Price);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating price in inventory for token {Token}", price.TokenName);
                }
            }
        }



        #region RZUSD PRICE

        public async Task<decimal> GetRZUSDPriceAsync()
        {
            if (
                _rZUSDPriceStorage.Price > 0 &&
                DateTime.UtcNow - _rZUSDPriceStorage.LastUpdate < TimeSpan.FromMinutes(5)
            )
            {
                return _rZUSDPriceStorage.Price;
            }

            await _priceLock.WaitAsync();

            try
            {
                if (
                    _rZUSDPriceStorage.Price > 0 &&
                    DateTime.UtcNow - _rZUSDPriceStorage.LastUpdate < TimeSpan.FromMinutes(5)
                )
                {
                    return _rZUSDPriceStorage.Price;
                }

                var result = await FetchRZUSDPriceAsync();

                if (result?.Price <= 0)
                    throw new BadRequestException();

                _rZUSDPriceStorage.Price = result.Price;
                _rZUSDPriceStorage.LastUpdate = DateTime.UtcNow;

                return result.Price;
            }
            finally
            {
                _priceLock.Release();
            }
        }

        public async Task<PriceResult> FetchRZUSDPriceAsync()
        {
            var tokenName = "RZUSD";
            var tokenCMCID = 35716;

            PriceResult result = null;

            try
            {
                var rzPriceResult = await _rZPriceService.FetchTokenPriceAsync(tokenName);
                if (rzPriceResult != null)
                {
                    result = new PriceResult
                    {
                        TokenName = rzPriceResult.TokenName,
                        TokenNetwork = rzPriceResult.TokenNetwork,
                        Price = rzPriceResult.Price
                    };

                    _rZUSDPriceStorage.Price = result.Price;
                    _rZUSDPriceStorage.LastUpdate = DateTime.UtcNow;
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching RZUSD price from RZPrice service, will fallback to CoinMarketCap");
            }

            try
            {
                result = await SyncOneFromCoinMarketCapWithIdAsync(tokenName, tokenCMCID);

                if (result != null && result.Price > 0)
                {
                    _rZUSDPriceStorage.Price = result.Price;
                    _rZUSDPriceStorage.LastUpdate = DateTime.UtcNow;
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching RZUSD price from CoinMarketCap");
            }

            if (_rZUSDPriceStorage.Price > 0)
            {
                _logger.LogWarning("Using cached RZUSD price: {Price}", _rZUSDPriceStorage.Price);
                return new PriceResult
                {
                    TokenName = tokenName,
                    TokenNetwork = "BEP20",
                    Price = _rZUSDPriceStorage.Price
                };
            }

            throw new BadRequestException("Failed to fetch RZUSD price from all sources");
        }

        #endregion


        #region CoinMarketCap Price
        public async Task<PriceResult?> SyncOneFromCoinMarketCapWithIdAsync(string tokenName, long cmcId)
        {
            try
            {

                if (cmcId <= 0) return null;

                string url = "https://pro-api.coinmarketcap.com/v1/cryptocurrency/quotes/latest" + $"?id={cmcId}&convert=USD";


                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-CMC_PRO_API_KEY", _priceSetting.CMCApiKey);
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(jsonString);


                var data = doc.RootElement.GetProperty("data");

                var id = cmcId.ToString();


                if (!data.TryGetProperty(id, out var tokenData)) return null;


                var quote = tokenData.GetProperty("quote").GetProperty("USD");
                decimal price = quote.GetProperty("price").GetDecimal();
                decimal change24h = quote.GetProperty("percent_change_24h").GetDecimal();


                return new PriceResult
                {
                    TokenName = tokenName,
                    TokenNetwork = "BEP20",
                    Price = price
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error syncing price for CMC ID {CMCId}",
                    cmcId
                );

                return null;
            }
        }

        private async Task<List<PriceResult>> SyncAllPricesFromCoinMarketCapAsync(List<AvailableTokenData> tokensData)
        {
            try
            {
                var tokens = tokensData
                    .Where(t => t.SyncPrice)
                    .Where(t => t.CMCID > 0)
                    .ToList();

                var allTokens = tokens
                    .GroupBy(t => new { t.CMCID, t.Network })
                    .Select(g => g.First())
                    .ToList();

                if (!allTokens.Any())
                    return new List<PriceResult>();

                var ids = allTokens
                    .Select(t => t.CMCID)
                    .Distinct()
                    .ToList();

                string idQuery = string.Join(",", ids);

                string url =
                    $"https://pro-api.coinmarketcap.com/v2/cryptocurrency/quotes/latest?id={idQuery}&convert=USD";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-CMC_PRO_API_KEY", _priceSetting.CMCApiKey);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(jsonString);

                var data = doc.RootElement.GetProperty("data");

                var results = new List<PriceResult>();

                foreach (var token in allTokens)
                {
                    var id = token.CMCID.ToString();

                    if (!data.TryGetProperty(id, out var tokenData))
                        continue;

                    var quote = tokenData
                        .GetProperty("quote")
                        .GetProperty("USD");

                    decimal price = quote.GetProperty("price").GetDecimal();

                    results.Add(new PriceResult
                    {
                        TokenName = token.Name,
                        TokenNetwork = token.Network,
                        Price = Math.Round(price, token.PriceDecimalPlaces),
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing all prices");
                return new List<PriceResult>();
            }
        }

        #endregion


        public async Task<EffectivePriceResult> CalculateEffectivePriceAsync(string tokenName, decimal assetQuantity, decimal USDTAmount)
        {
            // Validate input parameters
            if (assetQuantity <= 0) throw new BadRequestException("Token quantity must be greater than zero.", nameof(assetQuantity));
            var priceData = await GetTokenPriceAsync(tokenName);


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
                    Price = price,
                    Impact = priceImpact
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calculating effective price for {Token}", priceData.TokenName);
                throw new BaseException("Failed to calculate effective price due to an unexpected error.");
            }
        }
        private async Task<PriceResult> GetTokenPriceAsync(string tokenName)
        {
            if (_inventoryStorage.TryGetValue(tokenName.ToUpper(), out var existingInventoryData))
            {
                if (existingInventoryData?.Price != null && existingInventoryData.Price.Price > 0)
                {
                    return existingInventoryData.Price;
                }
            }

            try
            {
                var rzPriceResult = await _rZPriceService.FetchTokenPriceAsync(tokenName);
                if (rzPriceResult != null)
                {
                    return new PriceResult
                    {
                        TokenName = rzPriceResult.TokenName,
                        TokenNetwork = rzPriceResult.TokenNetwork,
                        Price = rzPriceResult.Price
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching price from RZPrice service for {Token}, will try CoinMarketCap", tokenName);
            }

            try
            {
                var token = _availableTokenDatas.FirstOrDefault(t => t.Name == tokenName.ToUpper());
                if (token != null && token.CMCID > 0)
                {
                    return await SyncOneFromCoinMarketCapWithIdAsync(tokenName, token.CMCID);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching price from CoinMarketCap for {Token}", tokenName);
            }

            _logger.LogWarning("Could not fetch price for token {Token} from any source", tokenName);
            return null;
        }

    }

}
