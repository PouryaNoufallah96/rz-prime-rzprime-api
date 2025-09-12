using RZPrime.Domain.Collections;

namespace RZPrime.Services._BlockChainWebSocket.DTOs
{
    public class NetworkEventLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public BlockchainEventType EventType { get; set; }
        public string Hash { get; set; }
        public long? BlockNumber { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Data { get; set; }
    }
}
