using RZPrime.Utilities.Exceptions.Common;
using RZPrime.Utilities.Services.Contracts;
using static RZPrime.Utilities.Constants.RegisterMode;

namespace RZPrime.Utilities.Services
{
    public class NonceService(int capacity = 1000000) : INonceService, IScopedDependency
    {
        private readonly HashSet<string> h = [];
        private readonly Queue<string> q = new();

        public bool Contains(string item) => h.Contains(item);
        public void Add(string item)
        {
            if (Contains(item))
                throw new BadRequestException();

            h.Add(item);
            q.Enqueue(item);

            if (q.Count > capacity)
                h.Remove(q.Dequeue());
        }
    }
}
