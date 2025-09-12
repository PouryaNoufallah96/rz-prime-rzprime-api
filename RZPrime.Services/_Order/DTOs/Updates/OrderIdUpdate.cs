using RZPrime.Utilities.Attributes;

namespace RZPrime.Services._Order.DTOs.Updates
{
    public class OrderIdUpdate
    {
       [StringInputValidation(maxLength:100)] public string OrderId { get; set; }
    }
}
