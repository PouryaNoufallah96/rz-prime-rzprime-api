using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.MongoDatabase.Documents;

namespace RZPrime.Domain.Collections
{
    [MonjoCollectionName("Inventories")]
    public class Inventory : BaseDocument
    {
        public string InventoryId { get; set; } = Guid.NewGuid().ToString("N");
        public string TokenAddress { get; set; }
        public string TokenName { get; set; }
        public string TokenNetwork { get; set; } 
        public decimal InitialQuantity { get; set; }
    }
}
