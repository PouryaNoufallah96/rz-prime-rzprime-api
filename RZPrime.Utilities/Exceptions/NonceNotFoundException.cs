using RZPrime.Utilities.Enums;
using RZPrime.Utilities.Exceptions.Common;

namespace RZPrime.Utilities.Exceptions
{
    public class NonceNotFoundException : NotFoundException
    {
        public NonceNotFoundException()
          : base(ApiResultStatusCode.NonceNotFound ,ExceptionMessages.NonceNotFoundException)
        {
        }        
    }
}
