using RZPrime.Utilities.Enums;
using RZPrime.Utilities.Exceptions.Common;

namespace RZPrime.Utilities.Exceptions
{
    public class InvalidCaptchaException : BaseException
    {
        public InvalidCaptchaException()
           : base(ApiResultStatusCode.InvalidCaptcha)
        {
        }
        public InvalidCaptchaException(string message)
            : base(ApiResultStatusCode.InvalidCaptcha, message)
        {
        }

    }
}
