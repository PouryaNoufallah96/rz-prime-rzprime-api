//using Microsoft.AspNetCore.Mvc.Filters;
//using RZPrime.Utilities.Enums;
//using RZPrime.Utilities.Exceptions;
//using RZPrime.Utilities.Exceptions.Common;
//using RZPrime.Utilities.Extension;
//using RZPrime.Utilities.Utilities;

//namespace RZPrime.Utilities.Filters
//{
//    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
//    public class ActiveUserOnlyAttribute : Attribute, IAuthorizationFilter
//    {
//        public void OnAuthorization(AuthorizationFilterContext context)
//        {
//            var jwtSecurityToken = context.HttpContext.GetToken();

//            if (jwtSecurityToken == null)
//                throw new AuthorizationException("Authorization error");

//            var statusClaim = jwtSecurityToken.Claims
//                .FirstOrDefault(c => c.Type == Claims.UserStatus.ToDisplay());

//            if (statusClaim == null)
//                throw new BaseException(ApiResultStatusCode.Forbidden, "User status not found in token");

//            if (statusClaim.Value != "Active")
//                throw new BaseException(ApiResultStatusCode.Forbidden, "Please use the RZ Prime web application to submit new orders, apply changes or complete transactions.");
//        }
//    }
//}
