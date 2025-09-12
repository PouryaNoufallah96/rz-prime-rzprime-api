using RZPrime.Utilities.Enums;
using RZPrime.Utilities.Exceptions.Common;
using System.Net;

namespace RZPrime.Utilities.Exceptions
{
    public class TooManyRequestsException : BaseException
    {
        public TooManyRequestsException()
           : base(ApiResultStatusCode.TooManyRequests)
        {
        }

        public TooManyRequestsException(string message)
            : base(ApiResultStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests, message)
        {
        }
    }
}
