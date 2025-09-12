using RZPrime.Utilities.Enums;
using RZPrime.Utilities.Exceptions.Common;

namespace RZPrime.Utilities.Exceptions
{
    public class RoleNotFoundException : BaseException
    {
        public RoleNotFoundException()
            : base(ApiResultStatusCode.RoleNotFound)
        {
        }
        public RoleNotFoundException(string message)
            : base(ApiResultStatusCode.RoleNotFound, System.Net.HttpStatusCode.NotFound, message)
        {
        }

    }
}
