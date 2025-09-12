using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using RZPrime.Services._PancakeSwap;
using System.Numerics;
using static RZPrime.Utilities.Constants.RegisterMode;

[Struct("Call")]
public class MulticallCall
{
    [Parameter("address", "target", 1)]
    public string Target { get; set; }

    [Parameter("bytes", "callData", 2)]
    public byte[] CallData { get; set; }
}

[Struct("Result")]
public class MulticallResult
{
    [Parameter("bool", "success", 1)]
    public bool Success { get; set; }

    [Parameter("bytes", "returnData", 2)]
    public byte[] ReturnData { get; set; }
}

[FunctionOutput]
public class AggregateOutput
{
    [Parameter("uint256", "blockNumber", 1)]
    public BigInteger BlockNumber { get; set; }

    [Parameter("bytes[]", "returnData", 2)]
    public List<byte[]> ReturnData { get; set; }
}


[Function("aggregate", typeof(AggregateOutput))]
public class AggregateFunction : FunctionMessage
{
    [Parameter("tuple[]", "calls", 1)]
    public List<MulticallCall> Calls { get; set; }
}

[FunctionOutput]
public class TryAggregateOutput : IFunctionOutputDTO
{
    [Parameter("tuple[]", "returnData", 1)]
    public List<MulticallResult> ReturnData { get; set; }
}

[Function("tryAggregate", typeof(TryAggregateOutput))]
public class TryAggregateFunction : FunctionMessage
{
    [Parameter("bool", "requireSuccess", 1)]
    public bool RequireSuccess { get; set; }

    [Parameter("tuple[]", "calls", 2)]
    public List<MulticallCall> Calls { get; set; }
}



public class MulticallService : IMulticallService, IScopedDependency
{
    private readonly Web3 _web3;
    private readonly string _multicallAddress;
    private readonly ContractHandler _multicallContractHandler;

    public MulticallService()
    {
        var rpcUrl = "https://bsc-dataseed.binance.org/";
        _web3 = new Web3(rpcUrl);

        _multicallAddress = "0xcA11bde05977b3631167028862bE2a173976CA11";
        _multicallContractHandler = _web3.Eth.GetContractHandler(_multicallAddress);
    }

    public async Task<List<byte[]>> ExecuteCallsAsync(List<MulticallCall> calls)
    {
        var aggregateFunction = new AggregateFunction { Calls = calls };

        try
        {
            var result = await _multicallContractHandler
                .QueryAsync<AggregateFunction, AggregateOutput>(aggregateFunction);
            return result.ReturnData;
        }
        catch
        {
            var callData = _multicallContractHandler.GetFunction<AggregateFunction>().GetData(aggregateFunction);
            var callInput = new CallInput { To = _multicallAddress, Data = callData };
            var callResult = await _web3.Eth.Transactions.Call.SendRequestAsync(callInput);
            var result = _multicallContractHandler.GetFunction<AggregateFunction>()
                .DecodeDTOTypeOutput<AggregateOutput>(callResult);
            return result.ReturnData;
        }
    }

    public async Task<List<MulticallResult>> ExecuteCallsTryAsync(List<MulticallCall> calls, bool requireSuccess = false)
    {
        var fn = new TryAggregateFunction { RequireSuccess = requireSuccess, Calls = calls };

        try
        {
            var res = await _multicallContractHandler
                .QueryAsync<TryAggregateFunction, TryAggregateOutput>(fn);
            return res.ReturnData;
        }
        catch
        {
            var callData = _multicallContractHandler.GetFunction<TryAggregateFunction>().GetData(fn);
            var callInput = new CallInput { To = _multicallAddress, Data = callData };
            var callResult = await _web3.Eth.Transactions.Call.SendRequestAsync(callInput);
            var decoded = _multicallContractHandler.GetFunction<TryAggregateFunction>()
                .DecodeDTOTypeOutput<TryAggregateOutput>(callResult);
            return decoded.ReturnData;
        }
    }
}

