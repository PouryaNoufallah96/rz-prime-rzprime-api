using RZPrime.Utilities.Enums;
using RZPrime.Utilities.Exceptions.Common;

namespace RZPrime.Utilities.Exceptions
{
    public class AuthorizationException : BaseException
    {
        public AuthorizationException()
           : base(ApiResultStatusCode.UnAuthorized)
        {
        }
        public AuthorizationException(string message)
            : base(ApiResultStatusCode.UnAuthorized, System.Net.HttpStatusCode.Unauthorized, message)
        {
        }
    }
}
