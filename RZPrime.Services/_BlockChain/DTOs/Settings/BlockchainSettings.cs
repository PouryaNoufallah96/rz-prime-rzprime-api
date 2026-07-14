using System.Numerics;

namespace RZPrime.Services._BlockChain.DTOs.Settings
{

    public class BlockChainSettings
    {
        public string RpcUrl { get; set; }
        public string RpcUrl2 { get; set; }
        public string ContractAddress { get; set; }
        public string PrivateKey { get; set; }
        public string PublicAddress { get; set; }
        public long ChainId { get; set; }
        public int TransactionTimeoutSeconds { get; set; }

        public string WsUrl { get; set; }
        public string WsUrl2 { get; set; }
        public int ReconnectInterval { get; set; } = 5;
        public int MaxReconnectAttempts { get; set; } = 30;
        public int HeartbeatInterval { get; set; } = 30;
        public int ConnectionTimeout { get; set; } = 10;
        public int SubscriptionTimeout { get; set; } = 30;

        public string DefaultGasLimit { get; set; }
        public string DefaultGasPriceGwei { get; set; }
        public string MinGasPriceGwei { get; set; }
        public string MaxGasPriceGwei { get; set; }

        public BigInteger GetDefaultGasLimit() => BigInteger.Parse(DefaultGasLimit);
        public BigInteger GetDefaultGasPriceGwei() => BigInteger.Parse(DefaultGasPriceGwei);
        public BigInteger GetMinGasPriceGwei() => BigInteger.Parse(MinGasPriceGwei);
        public BigInteger GetMaxGasPriceGwei() => BigInteger.Parse(MaxGasPriceGwei);
    }
}
