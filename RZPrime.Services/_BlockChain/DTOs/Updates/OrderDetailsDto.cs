using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using ParameterAttribute = Nethereum.ABI.FunctionEncoding.Attributes.ParameterAttribute;

namespace RZPrime.Services._BlockChain.DTOs
{
    [FunctionOutput]
    public class OrderOutputDto : IFunctionOutputDTO
    {
        [Parameter("address", "buyToken", 1)]
        public virtual string BuyToken { get; set; }

        [Parameter("uint256", "tokenAmount", 2)]
        public virtual BigInteger TokenAmount { get; set; }

        [Parameter("uint256", "payAmount", 3)]
        public virtual BigInteger PayAmount { get; set; }

        [Parameter("uint256", "endAt", 4)]
        public virtual BigInteger EndAt { get; set; }

        [Parameter("uint8", "status", 5)]
        public virtual byte Status { get; set; }
    }

    public class OrderDetails
    {
        public string BuyToken { get; set; }
        public BigInteger TokenAmount { get; set; }
        public BigInteger PayAmount { get; set; }
        public long EndAtTimestamp { get; set; }
        public OrderStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public bool IsExpired { get; set; }
        public decimal TokenAmountHumanReadable { get; set; } 
        public decimal PayAmountHumanReadable { get; set; } 
        public DateTime EndAtDateTimeUtc { get; set; }
    }
}
