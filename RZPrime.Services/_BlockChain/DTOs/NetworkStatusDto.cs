using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace RZPrime.Services._BlockChain.DTOs
{
    public class NetworkStatusDto
    {
        public bool Success { get; set; }
        public BigInteger NetworkId { get; set; }
        public BigInteger LatestBlockNumber { get; set; }
        public BigInteger LatestBlockTimestamp { get; set; }
        public BigInteger GasPriceInWei { get; set; }
        public decimal GasPriceInGwei => Web3.Convert.FromWei(GasPriceInWei, Nethereum.Util.UnitConversion.EthUnit.Gwei);
        public bool IsConnected { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
