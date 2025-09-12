using RZPrime.Utilities.Enums;
using RZPrime.Utilities.Exceptions.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RZPrime.Utilities.Exceptions
{
    public class InsufficientBalanceException : BadRequestException
    {
        public InsufficientBalanceException()
          : base(ApiResultStatusCode.InsufficientBalance, ExceptionMessages.NonceNotFoundException)
        {
        }
    }
}
