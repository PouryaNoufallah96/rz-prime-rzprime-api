
using RZPrime.Utilities.Attributes;

namespace RZPrime.Services._User.DTOs.Updates
{
    public class AppNonceRequest
    {
        [StringInputValidation(maxLength: 200)] public string WalletAddress { get; set; }
        [StringInputValidation(maxLength: 500,minLength:3)] public string Marker { get; set; }
        [StringInputValidation(maxLength: 50)] public string ClientId { get; set; }
        [StringInputValidation(maxLength: 50)] public string ClientSecret { get; set; }
    }

    public class WebNonceRequest 
    {
        [StringInputValidation(maxLength: 200,isRequired:false)] public string WalletAddress { get; set; } = null;
        [StringInputValidation(maxLength: 50)] public string ClientId { get; set; }
        [StringInputValidation(maxLength: 50)] public string ClientSecret { get; set; }
    }

    public class NonceRequest
    {
        [StringInputValidation(maxLength:200)] public string WalletAddress { get; set; }
        [StringInputValidation(maxLength: 500, isRequired: false)] public string Marker { get; set; } = null;
        [StringInputValidation(maxLength: 50)] public string ClientId { get; set; }
        [StringInputValidation(maxLength: 50)] public string ClientSecret { get; set; }
    }
}
