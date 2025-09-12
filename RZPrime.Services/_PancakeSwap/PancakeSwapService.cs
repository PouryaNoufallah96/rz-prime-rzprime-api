using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Web3;
using RZPrime.Services._PancakeSwap.DTOs;
using RZPrime.Services._Price.DTOs.Settings;
using RZPrime.Utilities.Exceptions.Common;
using System.Numerics;
using static RZPrime.Utilities.Constants.RegisterMode;
using Nethereum.Hex.HexConvertors.Extensions;


namespace RZPrime.Services._PancakeSwap
{
    public class PancakeSwapService(AvailableTokensSettings _availableTokenData, IMulticallService _multicallService) : IPancakeSwapService, IScopedDependency
    {
        private readonly Web3 _web3 = new Web3("https://bsc-dataseed.binance.org/");
        private const string RouterAddress = "0x10ED43C718714eb63d5aA57B78B54704E256024E"; // PancakeSwap V2 Router

        [FunctionOutput]
        public class GetAmountsOutOutputDTO : IFunctionOutputDTO
        {
            [Parameter("uint256[]", "amounts", 1)]
            public List<BigInteger> Amounts { get; set; }
        }


        [Function("getAmountsOut", "uint256[]")]
        public class GetAmountsOutFunction : FunctionMessage
        {
            [Parameter("uint256", "amountIn", 1)]
            public BigInteger AmountIn { get; set; }

            [Parameter("address[]", "path", 2)]
            public List<string> Path { get; set; }
        }


        [Function("decimals", "uint8")]
        public class DecimalsFunction : FunctionMessage { }


        public async Task<decimal> GetMultiCallOptimalSwapAmountInBSCAsync(GetSwapAmountUpdate update)
        {
            var tokenData = ValidateToken(update.TokenName);
            var usdtAmount = update.USDTAmount;

            var allPathsResults = await FindAllSinglePathsMultiCallResults(usdtAmount, tokenData);

            if (!allPathsResults.Any()) return 0;

            var bestSinglePathResult = allPathsResults.First();
            decimal bestOverallAmount = bestSinglePathResult.Amount;

            if (allPathsResults.Count > 1)
            {
                var path1 = allPathsResults[0].Path;
                var path2 = allPathsResults[1].Path;
                var decimalsOut = tokenData.PriceDecimalPlaces;

                var splitsToTest = new List<decimal> { 0.5m, 0.7m, 0.3m, 0.2m, 0.1m };
                var calls = new List<MulticallCall>();

                var routerContractHandler = _web3.Eth.GetContractHandler(RouterAddress);
                var getAmountsOutFunctionHandler = routerContractHandler.GetFunction<GetAmountsOutFunction>();

                foreach (var p in splitsToTest)
                {
                    // برای مسیر اول
                    var function1 = new GetAmountsOutFunction
                    {
                        AmountIn = Web3.Convert.ToWei(usdtAmount * p, 18),
                        Path = path1
                    };
                    calls.Add(new MulticallCall
                    {
                        Target = RouterAddress,
                        CallData = getAmountsOutFunctionHandler.GetData(function1).HexToByteArray()
                    });

                    // برای مسیر دوم
                    var function2 = new GetAmountsOutFunction
                    {
                        AmountIn = Web3.Convert.ToWei(usdtAmount * (1 - p), 18),
                        Path = path2
                    };
                    calls.Add(new MulticallCall
                    {
                        Target = RouterAddress,
                        CallData = getAmountsOutFunctionHandler.GetData(function2).HexToByteArray()
                    });
                }

                var results = await _multicallService.ExecuteCallsTryAsync(calls);

                decimal bestSplitAmount = 0;
                for (int i = 0; i < splitsToTest.Count; i++)
                {
                    try
                    {
                        int idx1 = i * 2;
                        int idx2 = i * 2 + 1;

                        decimal out1 = 0, out2 = 0;

                        if (idx1 < results.Count && results[idx1]?.Success == true && results[idx1].ReturnData?.Length > 0)
                        {
                            var decoded1 = getAmountsOutFunctionHandler
                                .DecodeDTOTypeOutput<GetAmountsOutOutputDTO>(results[idx1].ReturnData.ToHex());
                            out1 = Web3.Convert.FromWei(decoded1.Amounts.Last(), decimalsOut);
                        }

                        if (idx2 < results.Count && results[idx2]?.Success == true && results[idx2].ReturnData?.Length > 0)
                        {
                            var decoded2 = getAmountsOutFunctionHandler
                                .DecodeDTOTypeOutput<GetAmountsOutOutputDTO>(results[idx2].ReturnData.ToHex());
                            out2 = Web3.Convert.FromWei(decoded2.Amounts.Last(), decimalsOut);
                        }

                        var splitOutput = out1 + out2;
                        //Console.WriteLine($"Split {splitsToTest[i] * 100}%/{100 - splitsToTest[i] * 100}%: {splitOutput}");

                        if (splitOutput > bestSplitAmount)
                            bestSplitAmount = splitOutput;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing split {i}: {ex.Message}");
                    }
                }

                if (bestSplitAmount > bestOverallAmount)
                {
                    //Console.WriteLine($"\nSplit routing found a better rate: {bestSplitAmount} vs {bestOverallAmount}");
                    bestOverallAmount = bestSplitAmount;
                }
                else
                {
                    //Console.WriteLine($"\nSingle path routing was optimal: {bestOverallAmount}");
                }
            }

            return bestOverallAmount;
        }

        private async Task<List<(decimal Amount, List<string> Path)>> FindAllSinglePathsMultiCallResults(decimal usdtAmount, AvailableTokenData tokenData)
        {
            var usdtAddress = "0x55d398326f99059fF775485246999027B3197955";
            var tokenOutAddress = tokenData.Address;
            var tokenOutDecimals = tokenData.PriceDecimalPlaces;

            var stableTokens = new Dictionary<string, int>
            {
                { "0x55d398326f99059fF775485246999027B3197955", 18 }, // USDT
                { "0xe9e7CEA3DedcA5984780Bafc599bD69ADd087D56", 18 }, // BUSD
                { "0x8ac76a51cc950d9822d68b83fe1ad97b32cd580d", 18 }, // USDC
                //{ "0x1AF3F329e8BE154074D8769D1FFa4eE058B1DBc3", 18 }, // DAI
            };

            var intermediateTokens = new Dictionary<string, int>
            {
                { "0xbb4cdb9cbd36b01bd1cbaebf2de08d9173bc095c", 18 }, // WBNB
                { "0xbb73BB2505AC4643d5C0a99c2A1F34B3DfD09D11", 9 },  // MGC
                { "0x7130d2a12b9bcBFAe4f2634d864A1Ee1Ce3Ead9c", 18 }, // BTCB
                //{ "0x2170Ed0880ac9A755fd29B2688956BD959F933F8", 18 }, // ETH
                { "0xC4A1cc5cA8955a4650BDC109bddf110E33a1e344", 18 }, // RZUSD
            }.Concat(stableTokens).ToDictionary(x => x.Key, x => x.Value);

            var pathsToTest = new List<List<string>>();
            //{
            //    new() { usdtAddress, tokenOutAddress }
            //};

            foreach (var stable in stableTokens.Keys)
            {
                pathsToTest.Add([stable, tokenOutAddress]);
            }

            foreach (var intermediate in intermediateTokens)
            {
                if (!intermediate.Key.Equals(tokenOutAddress, StringComparison.OrdinalIgnoreCase))
                    pathsToTest.Add([usdtAddress, intermediate.Key, tokenOutAddress]);
            }

            foreach (var x in intermediateTokens.Keys)
            {
                foreach (var y in intermediateTokens.Keys)
                {
                    if (x == y || x == tokenOutAddress || y == tokenOutAddress) continue;

                    pathsToTest.Add([usdtAddress, x, y, tokenOutAddress]);
                }
            }
            pathsToTest = pathsToTest.Where(p => p.Count <= 4).ToList();

            var calls = new List<MulticallCall>();

            var routerContractHandler = _web3.Eth.GetContractHandler(RouterAddress);
            var getAmountsOutFunctionHandler = routerContractHandler.GetFunction<GetAmountsOutFunction>();

            foreach (var path in pathsToTest)
            {
                try
                {
                    var function = new GetAmountsOutFunction
                    {
                        AmountIn = Web3.Convert.ToWei(usdtAmount, 18),
                        Path = path
                    };

                    var callData = getAmountsOutFunctionHandler.GetData(function);

                    calls.Add(new MulticallCall
                    {
                        Target = RouterAddress,
                        CallData = callData.HexToByteArray()
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating call for path {string.Join("->", path)}: {ex.Message}");
                }
            }

            if (!calls.Any())
                return [];

            try
            {
                var results = await _multicallService.ExecuteCallsTryAsync(calls); 
                var output = new List<(decimal Amount, List<string> Path)>();

                for (int i = 0; i < pathsToTest.Count; i++)
                {
                    try
                    {
                        if (i >= results.Count || results[i] == null) continue;

                        var r = results[i];

                        if (!r.Success || r.ReturnData == null || r.ReturnData.Length == 0) continue;

                        var decoded = getAmountsOutFunctionHandler
                            .DecodeDTOTypeOutput<GetAmountsOutOutputDTO>(r.ReturnData.ToHex());

                        var amountOut = Web3.Convert.FromWei(decoded.Amounts.Last(), tokenOutDecimals);

                        if (amountOut > 0)
                        {
                            output.Add((amountOut, pathsToTest[i]));
                            //Console.WriteLine($"Path: {string.Join(" -> ", pathsToTest[i])}, Amount: {amountOut}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error decoding result for path {i}: {ex.Message}");
                    }
                }

                return output.OrderByDescending(r => r.Amount).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Multicall execution failed: {ex.Message}");
                return [];
            }
        }





        /// <summary>
        /// split
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        public async Task<decimal> GetOptimalSwapAmountInBSCAsync(GetSwapAmountUpdate update)
        {
            var tokenData = ValidateToken(update.TokenName);
            var usdtAmount = update.USDTAmount;
            var router = _web3.Eth.GetContractHandler(RouterAddress);

            var allPathsResults = await FindAllSinglePathsResults(router, usdtAmount, tokenData);

            if (!allPathsResults.Any()) return 0;

            var bestSinglePathResult = allPathsResults.First();
            decimal bestOverallAmount = bestSinglePathResult.Amount;

            if (allPathsResults.Count > 1)
            {
                var path1 = allPathsResults[0].Path;
                var path2 = allPathsResults[1].Path;
                var decimalsOut = tokenData.PriceDecimalPlaces;



                var splitsToTest = new List<decimal> { 0.5m, 0.7m, 0.3m, 0.2m }; // 50%, 70%, 30% for path 1
                var tasks = new List<Task<decimal>>();

                foreach (var p in splitsToTest)
                {
                    tasks.Add(CalculateOutputForPathAsync(router, path1, usdtAmount * p, 18, decimalsOut));
                    tasks.Add(CalculateOutputForPathAsync(router, path2, usdtAmount * (1 - p), 18, decimalsOut));
                }

                var results = await Task.WhenAll(tasks);

                decimal bestSplitAmount = 0;
                for (int i = 0; i < splitsToTest.Count; i++)
                {
                    decimal splitOutput = results[i * 2] + results[i * 2 + 1];

                    if (splitOutput > bestSplitAmount)
                    {
                        bestSplitAmount = splitOutput;
                    }
                }

                if (bestSplitAmount > bestOverallAmount)
                {
                    Console.WriteLine($"\nSplit routing found a better rate: {bestSplitAmount}");
                    bestOverallAmount = bestSplitAmount;
                }
                else
                {
                    Console.WriteLine($"\nSingle path routing was optimal.");
                }
            }

            return bestOverallAmount;
        }

        private async Task<List<(decimal Amount, List<string> Path)>> FindAllSinglePathsResults(IContractHandler router, decimal usdtAmount, AvailableTokenData tokenData)
        {
            var usdtAddress = "0x55d398326f99059fF775485246999027B3197955";
            var tokenOutAddress = tokenData.Address;
            var tokenOutDecimals = tokenData.PriceDecimalPlaces;

            var intermediateTokens = new Dictionary<string, int>
            {
                { "0xbb4cdb9cbd36b01bd1cbaebf2de08d9173bc095c", 18 }, // WBNB
                { "0xC4A1cc5cA8955a4650BDC109bddf110E33a1e344", 18 }, // RZUSD
                { "0xbb73BB2505AC4643d5C0a99c2A1F34B3DfD09D11", 9 },  // MGC
                { "0xe9e7CEA3DedcA5984780Bafc599bD69ADd087D56", 18 } , // BUSD
                { "0x7130d2A12B9BCbFAe4f2634d864A1Ee1Ce3Ead9c", 18 }, // BTCB
                { "0x2170Ed0880ac9A755fd29B2688956BD959F933F8", 18 }  // ETH
            };

            var wbnbAddress = "0xbb4cdb9cbd36b01bd1cbaebf2de08d9173bc095c";
            var mgcAddress = "0xbb73BB2505AC4643d5C0a99c2A1F34B3DfD09D11";

            var pathsToTest = new List<List<string>>();
            pathsToTest.Add(new List<string> { usdtAddress, tokenOutAddress });
            foreach (var intermediate in intermediateTokens)
            {
                if (intermediate.Key.Equals(tokenOutAddress, StringComparison.OrdinalIgnoreCase)) continue;
                pathsToTest.Add(new List<string> { usdtAddress, intermediate.Key, tokenOutAddress });
            }

            if (!tokenOutAddress.Equals(wbnbAddress, StringComparison.OrdinalIgnoreCase) &&
            !tokenOutAddress.Equals(mgcAddress, StringComparison.OrdinalIgnoreCase))
            {
                pathsToTest.Add(new List<string> { usdtAddress, wbnbAddress, mgcAddress, tokenOutAddress });
            }

            var results = new List<(decimal Amount, List<string> Path)>();
            foreach (var path in pathsToTest)
            {
                var amount = await CalculateOutputForPathAsync(router, path, usdtAmount, 18, tokenOutDecimals);
                if (amount > 0)
                {
                    results.Add((amount, path));
                }
            }
            return results.OrderByDescending(r => r.Amount).ToList();
        }

        private async Task<decimal> CalculateOutputForPathAsync(IContractHandler router, List<string> path, decimal amountIn, int decimalsIn, int tokenOutDecimals)
        {
            if (amountIn <= 0) return 0;
            try
            {
                var function = new GetAmountsOutFunction() { AmountIn = Web3.Convert.ToWei(amountIn, decimalsIn), Path = path };
                var result = await router.QueryAsync<GetAmountsOutFunction, List<BigInteger>>(function);
                return Web3.Convert.FromWei(result.Last(), tokenOutDecimals);
            }
            catch (Exception) { return 0; }
        }




        /// من در سرویس خودم برای محاسبه مقدار توکن خروجی از USDT،
        /// ابتدا تعداد اعشار توکن مقصد را از قرارداد آن می‌خوانم تا مقادیر 
        /// به شکل درست نمایش داده شوند. سپس مسیرهای ممکن شامل مسیر مستقیم USDT
        /// به توکن و مسیرهای واسطه‌ای مانند USDT → WBNB → توکن و USDT → BUSD → توکن بررسی می‌شوند تا بهترین مقدار 
        /// خروجی محاسبه شود. برای هر مسیر با استفاده از تابع getAmountsOut در قرارداد Router مقدار توکن خروجی محاسبه می‌شود و بهترین مقدار بین مسیرها انتخاب می‌شود. 
        /// در نهایت برای نزدیکی به مقدار واقعی UI PancakeSwap درصد کمی slippage لحاظ می‌کنم تا اختلاف احتمالی بین مقدار انتظار کاربر و مقدار واقعی تراکنش کاهش یابد. 
        /// نتیجه نهایی بهترین مقدار توکن دریافتی با مسیر بهینه و slippage اعمال شده است و تقریبا همان چیزی است که کاربر در PancakeSwap UI می‌بیند.
        /// </summary>
        /// <param name="usdtAmount"></param>
        /// <param name="tokenOut"></param>
        /// <param name="tokenDecimal"></param>
        /// <returns></returns>
        public async Task<decimal> GetSwapAmountInBSCAsync(GetSwapAmountUpdate update)
        {
            var tokenData = ValidateToken(update.TokenName);
            var usdtAmount = update.USDTAmount;

            var usdtAddress = "0x55d398326f99059fF775485246999027B3197955";
            var usdtDecimals = 18;
            var tokenOutDecimals = tokenData.PriceDecimalPlaces;

            var intermediateTokens = new Dictionary<string, int> // address, decimal
        {
            { "0xbb4cdb9cbd36b01bd1cbaebf2de08d9173bc095c", 18 }, // WBNB
            { "0xbb73BB2505AC4643d5C0a99c2A1F34B3DfD09D11", 9 },  // MGC
            //{ "0xe9e7CEA3DedcA5984780Bafc599bD69ADd087D56", 18 }  // BUSD (برای مثال)
        };

            var router = _web3.Eth.GetContractHandler(RouterAddress);
            decimal bestAmount = 0;

            //  بررسی مسیر مستقیم: USDT -TokenOut
            var directPath = new List<string> { usdtAddress, tokenData.Address };
            bestAmount = await CalculateOutput(router, directPath, usdtAmount, usdtDecimals, tokenOutDecimals, bestAmount);

            // بررسی مسیرهای واسطه‌ای
            foreach (var intermediate in intermediateTokens)
            {
                var path = new List<string> { usdtAddress, intermediate.Key, tokenData.Address };

                // جلوگیری از مسیرهای نامعتبر مانند USDT -> MGC -> MGC
                if (intermediate.Key.Equals(tokenData.Address, StringComparison.OrdinalIgnoreCase)) continue;

                bestAmount = await CalculateOutput(router, path, usdtAmount, usdtDecimals, tokenOutDecimals, bestAmount);
            }

            var wbnbAddress = "0xbb4cdb9cbd36b01bd1cbaebf2de08d9173bc095c";
            var mgcAddress = "0xbb73BB2505AC4643d5C0a99c2A1F34B3DfD09D11";

            if (!tokenData.Address.Equals(wbnbAddress, StringComparison.OrdinalIgnoreCase) &&
                !tokenData.Address.Equals(mgcAddress, StringComparison.OrdinalIgnoreCase))
            {
                var multiPath = new List<string> { usdtAddress, wbnbAddress, mgcAddress, tokenData.Address };
                bestAmount = await CalculateOutput(router, multiPath, usdtAmount, usdtDecimals, tokenOutDecimals, bestAmount);
            }



            decimal slippage = CalculateDynamicSlippage(usdtAmount);
            bestAmount *= (1 - slippage);
            bestAmount -= CalculateTransactionFee(bestAmount);

            return bestAmount;
        }


        /// <summary>
        /// helper function for find best amount
        /// </summary>
        /// <param name="router"></param>
        /// <param name="path"></param>
        /// <param name="amountIn"></param>
        /// <param name="decimalsIn"></param>
        /// <param name="tokenOutDecimals"></param>
        /// <param name="currentBest"></param>
        /// <returns></returns>
        private async Task<decimal> CalculateOutput(IContractHandler router, List<string> path,
                                            decimal amountIn, int decimalsIn,
                                            int tokenOutDecimals, decimal currentBest)
        {
            try
            {
                var function = new GetAmountsOutFunction()
                {
                    AmountIn = Web3.Convert.ToWei(amountIn, decimalsIn),
                    Path = path
                };

                var result = await router.QueryAsync<GetAmountsOutFunction, List<BigInteger>>(function);
                var outputAmount = Web3.Convert.FromWei(result[^1], tokenOutDecimals);

                Console.WriteLine($"Path: {string.Join(" → ", path)} | Output: {outputAmount}");

                return outputAmount > currentBest ? outputAmount : currentBest;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in path {string.Join(" → ", path)}: {ex.Message}");
                return currentBest;
            }
        }

        /// <summary>
        /// check for existing token by name and return token setting
        /// </summary>
        /// <param name="tokenName"></param>
        /// <returns></returns>
        /// <exception cref="BadRequestException"></exception>
        private AvailableTokenData ValidateToken(string tokenName)
        {
            var tokenData = _availableTokenData.FirstOrDefault(q => q.Name.Equals(tokenName, StringComparison.CurrentCultureIgnoreCase))
                ?? throw new BadRequestException($"Unsupported token name! {tokenName}");
            return tokenData;
        }



        private decimal CalculateDynamicSlippage(decimal amount)
        {
            if (amount > 10000) return 0.01m;    // 1% برای حجم بالا
            if (amount > 1000) return 0.005m;    // 0.5% برای حجم متوسط
            return 0.003m;                       // 0.3% برای حجم کم
        }

        private decimal CalculateTransactionFee(decimal amount)
        {
            const decimal pancakeFee = 0.0025m; // 0.25% fee
            return amount * pancakeFee;
        }

        private string CleanAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                return address;

            // حذف تمام فضاهای خالی از کل رشته
            var cleaned = address.Replace(" ", "");

            // حذف سایر کاراکترهای غیرمجاز
            cleaned = cleaned.Replace("\t", "")   // تب
                            .Replace("\n", "")   // خط جدید
                            .Replace("\r", "")   // carriage return
                            .Replace("?", "")
                            .Replace("'", "")
                            .Replace("\"", "")
                            .Trim()
                            .ToLower();

            // بررسی و اصلاح پیشوند 0x
            if (cleaned.StartsWith("0x"))
            {
                // اطمینان از اینکه فقط یک 0x در ابتدا وجود دارد
                cleaned = "0x" + cleaned.Substring(2).Replace("0x", "");
            }
            else
            {
                // اضافه کردن 0x اگر وجود ندارد
                cleaned = "0x" + cleaned;
            }


            return cleaned;
        }
    }
}


///// <summary>
///// محاسبه مقدار توکن خروجی بر اساس مقدار USDT ورودی
///// </summary>
//public async Task<decimal> GetTokenOutAmountAsync(decimal amountIn, string tokenIn, string tokenOut, int decimalsIn = 18)
//{
//    tokenIn = tokenIn.Trim();
//    tokenOut = tokenOut.Trim();

//    var tokenOutDecimals = await GetTokenDecimalsAsync(tokenOut);

//    var router = _web3.Eth.GetContractHandler(RouterAddress);
//    var function = new GetAmountsOutFunction()
//    {
//        AmountIn = Web3.Convert.ToWei(amountIn, decimalsIn),
//        Path = new List<string> { tokenIn, tokenOut }
//    };

//    var result = await router.QueryAsync<GetAmountsOutFunction, List<BigInteger>>(function);

//    var outputAmount = Web3.Convert.FromWei(result[^1], tokenOutDecimals);
//    return outputAmount;
//}
//[Function("getAmountsOut", "uint256[]")]
//public class GetAmountsOutFunction : FunctionMessage
//{
//    [Parameter("uint256", "amountIn", 1)]
//    public BigInteger AmountIn { get; set; }

//    [Parameter("address[]", "path", 2)]
//    public List<string> Path { get; set; }
//}

///// <summary>
///// محاسبه تعداد توکن مقصد بر اساس مقدار ورودی (Effective Price)
///// </summary>
///// <param name="amountIn">مقدار ورودی (مثلا 100 USDT)</param>
///// <param name="tokenIn">آدرس توکن ورودی (مثلا USDT)</param>
///// <param name="tokenOut">آدرس توکن خروجی (مثلا RZ)</param>
///// <param name="decimalsIn">تعداد اعشار توکن ورودی (مثلا USDT = 6)</param>
///// <param name="decimalsOut">تعداد اعشار توکن خروجی (مثلا RZ = 18)</param>
///// <returns>مقدار نهایی توکن خروجی</returns>
//public async Task<decimal> GetTokenOutAmountAsync(
//    decimal amountIn,
//    string tokenIn,
//    string tokenOut,
//    int decimalsIn = 18,
//    int decimalsOut = 18)
//{
//    var router = _web3.Eth.GetContractHandler(RouterAddress);

//    var function = new GetAmountsOutFunction()
//    {
//        AmountIn = Web3.Convert.ToWei(amountIn, decimalsIn),
//        Path = new List<string> { tokenIn, tokenOut }
//    };

//    var result = await router.QueryAsync<GetAmountsOutFunction, List<BigInteger>>(function);

//    var outputAmount = Web3.Convert.FromWei(result[^1], decimalsOut); // آخرین مقدار مسیر خروجی
//    return outputAmount;
//}

//public async Task<decimal> GetBestQuoteAsync(GetSwapAmountUpdate update)
//{
//    try
//    {
//        var tokenData = ValidateToken(update.TokenName);
//        var toTokenAddress = tokenData.Address;
//        var toDecimals = tokenData.PriceDecimalPlaces;
//        var amount = update.USDTAmount;
//        var fromTokenAddress = "0x55d398326f99059fF775485246999027B3197955";
//        var fromDecimals = 18;

//        // تبدیل مقدار به Wei
//        var amountIn = Web3.Convert.ToWei(amount, fromDecimals).ToString();

//        // درخواست به 1inch
//        var url = $"https://api.1inch.io/v5.0/56/quote?fromTokenAddress={fromTokenAddress}&toTokenAddress={toTokenAddress}&amount={amountIn}";
//        var response = await _httpClient.GetStringAsync(url);

//        using var doc = JsonDocument.Parse(response);
//        var toTokenAmount = doc.RootElement.GetProperty("toTokenAmount").GetString();

//        // تبدیل از Wei به decimal
//        var result = Web3.Convert.FromWei(BigInteger.Parse(toTokenAmount), toDecimals);
//        return result;
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine($"Error fetching quote: {ex.Message}");
//        return 0;
//    }
//}
//    public async Task<decimal> GetOptimalSwapAmountInBSCAsync2(GetSwapAmountUpdate update)
//{

//    var tokenData = ValidateToken(update.TokenName);
//    var usdtAmount = update.USDTAmount;

//    var usdtAddress = "0x55d398326f99059fF775485246999027B3197955";
//    var usdtDecimals = 18;
//    var tokenOutDecimals = tokenData.PriceDecimalPlaces;


//    var intermediateTokens = new Dictionary<string, int> // address , decimal
//    {
//        { "0xbb4cdb9cbd36b01bd1cbaebf2de08d9173bc095c" ,18}, // WBNB
//        { "0xbb73BB2505AC4643d5C0a99c2A1F34B3DfD09D11" ,9}, // MGC

//    };

//    var router = _web3.Eth.GetContractHandler(RouterAddress);
//    decimal bestAmount = 0;


//    var directPath = new List<string> { usdtAddress, tokenData.Address };
//    bestAmount = await CalculateOutput(router, directPath, usdtAmount,
//                             usdtDecimals, tokenOutDecimals, bestAmount);

//    foreach (var intermediate in intermediateTokens)
//    {
//        var intermediateDecimals = intermediate.Value;
//        var path = new List<string> { usdtAddress, intermediate.Key, tokenData.Address };
//        bestAmount = await CalculateOutput(router, path, usdtAmount,
//                                         usdtDecimals, tokenOutDecimals, bestAmount);
//    }

//    decimal slippage = CalculateDynamicSlippage(usdtAmount);
//    bestAmount *= (1 - slippage);

//    bestAmount -= CalculateTransactionFee(bestAmount);
//    return bestAmount;
//}