namespace RZPrime.Services._PancakeSwap
{
    public interface IMulticallService
    {
        Task<List<byte[]>> ExecuteCallsAsync(List<MulticallCall> calls);
        Task<List<MulticallResult>> ExecuteCallsTryAsync(List<MulticallCall> calls, bool requireSuccess = false);
    }
}
