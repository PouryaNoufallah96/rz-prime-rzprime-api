namespace RZPrime.Utilities.Services.Contracts
{
    public interface INonceService
    {
        void Add(string item);
        bool Contains(string item);
    }
}
