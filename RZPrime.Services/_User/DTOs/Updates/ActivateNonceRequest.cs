using RZPrime.Utilities.Attributes;

namespace RZPrime.Services._User.DTOs.Updates
{
    public class ActivateNonceRequest
    {
        [StringInputValidation(maxLength: 8, isRequired: false)] public string Code { get; set; }
        [StringInputValidation(maxLength: 200, isRequired: false)] public string Nonce { get; set; }
        [StringInputValidation(maxLength: 200,minLength:20)] public string WalletAddress { get; set; }
        [StringInputValidation(maxLength: 500,minLength:3)] public string Marker { get; set; }
        [StringInputValidation(maxLength: 50)] public string ClientId { get; set; }
        [StringInputValidation(maxLength: 50)] public string ClientSecret { get; set; }
    }
}
