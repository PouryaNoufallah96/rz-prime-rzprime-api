using RZPrime.Utilities.Enums;
using RZPrime.Utilities.Exceptions.Common;

namespace RZPrime.Utilities.Exceptions
{
    public class DuplicateException : BaseException
    {
        public DuplicateException()
           : base(ApiResultStatusCode.Duplicated)
        {
        }
        public DuplicateException(string message)
            : base(ApiResultStatusCode.Duplicated, message)
        {
        }
    }
}
