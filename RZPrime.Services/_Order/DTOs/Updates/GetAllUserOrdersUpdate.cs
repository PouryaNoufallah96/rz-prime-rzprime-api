using RZPrime.Domain.Collections;
using RZPrime.Utilities.DTOs;

namespace RZPrime.Services._Order.DTOs.Updates
{
    public class GetAllUserOrdersUpdate
    {
        public Pagination Pagination { get; set; }
        public List<OrderState> States { get; set; } = []; 
    }
}
