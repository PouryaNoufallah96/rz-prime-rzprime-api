namespace RZPrime.Services._PancakeSwap.DTOs
{
    public class GetSwapAmountUpdate
    {
        public decimal USDTAmount { get; set; }
        public string TokenName { get; set; } 
    }

    public class MulticallInput
    {
        public string Target { get; set; }
        public string CallData { get; set; }
    }

    public class MulticallOutput
    {
        public bool Success { get; set; }
        public string ReturnData { get; set; }
    }


    class PathComparer : IEqualityComparer<List<string>>
    {
        public bool Equals(List<string> x, List<string> y)
        {
            if (x == null || y == null) return false;
            if (x.Count != y.Count) return false;
            for (int i = 0; i < x.Count; i++)
                if (!x[i].Equals(y[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            return true;
        }

        public int GetHashCode(List<string> obj)
        {
            unchecked
            {
                int hash = 17;
                foreach (var s in obj)
                    hash = hash * 31 + (s?.ToLowerInvariant().GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
 