using RZPrime.Utilities.Enums;
using RZPrime.Utilities.Exceptions.Common;

namespace RZPrime.Utilities.Exceptions
{
    public class OAuthException : BaseException
    {
        public OAuthException()
           : base(ApiResultStatusCode.OAuth)
        {
        }
        public OAuthException(string message)
            : base(ApiResultStatusCode.OAuth, message)
        {
        }
    }
}
