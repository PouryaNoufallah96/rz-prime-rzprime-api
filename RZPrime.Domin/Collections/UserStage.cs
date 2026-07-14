using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.MongoDatabase.Documents;
using System.ComponentModel.DataAnnotations;

namespace RZPrime.Domain.Collections
{

    [MonjoCollectionName("UserStages")]
    public class UserStage : BaseDocument
    {
        public string UserStageId { get; set; } = Guid.NewGuid().ToString("N");
        public string UserPublicKey { get; set; }
        public string WalletAddress { get; set; }
        public UserStageType Stage { get; set; }
        public int AvailableDrop { get; set; } = 5;
    }
     
    public enum UserStageType
    {
        [Display(Name = "Regular")]
        Regular,

        [Display(Name = "Gold")]
        Gold,

        [Display(Name = "Premium")]
        Premium,

        [Display(Name = "X")]
        X
    } // at first user is regular

}
