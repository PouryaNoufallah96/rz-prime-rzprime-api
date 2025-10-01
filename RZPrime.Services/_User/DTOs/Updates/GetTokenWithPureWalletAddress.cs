using RZPrime.Utilities.Attributes;

namespace RZPrime.Services._User.DTOs.Updates
{
    public class GetTokenWithPureWalletAddress
    {
        [StringInputValidation] public string WalletAddress { get; set; }
        [StringInputValidation] public string ClientId { get; set; }
        [StringInputValidation] public string ClientSecret { get; set; }
    }
}
