using RZPrime.Utilities.Attributes;

namespace RZPrime.Services._Order.DTOs.Updates
{
    public class DropOrderUpdate
    {
        [StringInputValidation(maxLength:100)] public string OrderId { get; set; }
        [StringInputValidation(maxLength: 9000)] public string Signature { get; set; }
    }
}
 