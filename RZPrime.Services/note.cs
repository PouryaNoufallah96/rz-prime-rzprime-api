////using MathNet.Numerics;
////using Org.BouncyCastle.Pqc.Crypto.Lms;
////using static NPOI.HSSF.Util.HSSFColor;

////DEBUG = false
////DATA_BASE_HOST = vaultora - db.c14cy6ae4cvf.eu - central - 1.rds.amazonaws.com
////DATA_BASE_USER = admin
////DATA_BASE_PASSWORD = RZloan123qwe
////DATA_BASE_NAME = vaultora
////DATA_BASE_PORT = 3306
////FAST_API_SECRET_KEY = 09d25e094faa6ca2556c818166b7a9563b93f7099f6f0f4caa6cf63b88e8d3e7
////#REDIS HOST
////REDIS_HOST = redis
////REDIS_PORT=6379
////REDIS_PASSWORD=Gianx2025
////#CELERY CONF
////CELERY_ACCEPT_CONTENT=json
////CELERY_TASK_SERIALIZER=json
////CELERY_RESULT_SERIALIZER=json
////CELERY_TIMEZONE=UTC
////HTTPS_MODE=True
////CONTRACT_ADDRESS=0xB0D6B2805C09A37c762c3599fA932DC5Dc0D65c3
////MGC_TOKEN_ADDRESS=0xB0D6B2805C09A37c762c3599fA932DC5Dc0D65c3
////USDT_TOKEN_ADDRESS=0xbeeAF6AEa7D96015b27a3D5a11eB47D0cF330E16
////PRIVATE_KEY=7f0638379dc3d9449125eef6ee7df142aef0dfc1065a29cf1dcf97835d9a3b7c
////PUBLIC_ADDRESS=0xD549A49F31C2ff581dd9B7942079d9cFC73F4940
////HTTP_RPC=https://eth-sepolia.g.alchemy.com/v2/G2wfc-JVUnW9zf7UGE5qAEs8i2Rs3ADN
////WS_RPC = wss://sly-cosmological-county.ethereum-sepolia.quiknode.pro/71b145dee165b2419944c0092b27bd0289675c52
////DEFAULT_GAS_LIMIT = 500000
////GAS_PRICE_GWEI = 20
////MAX_GAS_PRICE_GWEI = 100
////NETWORK_ID = 11155111
////CONFIRMATION_BLOCKS = 1
////TRANSACTION_TIMEOUT = 300




//using Nethereum.ABI.FunctionEncoding.Attributes;
//using Nethereum.Contracts;
//using Nethereum.Web3;
//using Nethereum.RPC.Eth.DTOs;
//using System.Numerics;
//using Nethereum.Contracts.ContractHandlers;
//using RZPrime.Services._PancakeSwap;
//using static RZPrime.Utilities.Constants.RegisterMode;

//[Struct("Call")] // Struct برای Tuple
//public class MulticallCall
//{
//    [Parameter("address", "target", 1)]
//    public string Target { get; set; }

//    [Parameter("bytes", "callData", 2)]
//    public byte[] CallData { get; set; }
//}

//[FunctionOutput]
//public class AggregateOutput
//{
//    [Parameter("uint256", "blockNumber", 1)]
//    public BigInteger BlockNumber { get; set; }

//    [Parameter("bytes[]", "returnData", 2)]
//    public List<byte[]> ReturnData { get; set; }
//}

//[Function("aggregate", typeof(AggregateOutput))]
//public class AggregateFunction : FunctionMessage
//{
//    [Parameter("tuple[]", "calls", 1)]
//    public List<MulticallCall> Calls { get; set; }
//}



//public class MulticallService : IMulticallService, IScopedDependency
//{
//    private readonly Web3 _web3;
//    private readonly string _multicallAddress;
//    private readonly ContractHandler _multicallContractHandler;

//    public MulticallService()
//    {
//        var rpcUrl = "https://bsc-dataseed.binance.org/";
//        _web3 = new Web3(rpcUrl);

//        _multicallAddress = "0xcA11bde05977b3631167028862bE2a173976CA11";
//        _multicallContractHandler = _web3.Eth.GetContractHandler(_multicallAddress);
//    }

//    public async Task<List<byte[]>> ExecuteCallsAsync(List<MulticallCall> calls)
//    {
//        var aggregateFunction = new AggregateFunction { Calls = calls };

//        try
//        {
//            var result = await _multicallContractHandler.QueryAsync<AggregateFunction, AggregateOutput>(
//                aggregateFunction);

//            return result.ReturnData;
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"Multicall query failed: {ex.Message}");
//            return await ExecuteCallsFallback(aggregateFunction);
//        }
//    }

//    private async Task<List<byte[]>> ExecuteCallsFallback(AggregateFunction aggregateFunction)
//    {
//        var callData = _multicallContractHandler.GetFunction<AggregateFunction>().GetData(aggregateFunction);

//        var callInput = new CallInput
//        {
//            To = _multicallAddress,
//            Data = callData,
//            From = "0x0000000000000000000000000000000000000000"
//        };

//        var callResult = await _web3.Eth.Transactions.Call.SendRequestAsync(callInput);

//        var result = _multicallContractHandler.GetFunction<AggregateFunction>()
//            .DecodeDTOTypeOutput<AggregateOutput>(callResult);

//        return result.ReturnData;
//    }

//}