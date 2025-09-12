using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using RZPrime.Utilities.Extension;

namespace RZPrime.Utilities.Extension
{
    public static class HttpContextExtensions
    {
        public static JwtSecurityToken GetToken(this HttpContext httpContext)
            => (JwtSecurityToken)httpContext.Items["Token"];

        public static string GetClaim(this HttpContext httpContext, string claim)
            => httpContext.GetToken()?.GetClaim(claim)?.Value;

        public static string GetRequestIpv4(this IHttpContextAccessor context)
        {
            var userIP = context.HttpContext?.GetRequestIpv4();
            if (!string.IsNullOrEmpty(userIP)) return userIP;
            return context.HttpContext?.Connection.RemoteIpAddress.ToString();
        }

        public static string GetRequestIpv4(this HttpContext context)
        {
            var userIP = context.Request.Headers.Where(q => q.Key == "X-Forwarded-For").Select(q => q.Value)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(userIP)) return userIP;
            return context.Connection.RemoteIpAddress.ToString();
        }

        public static string GetRequestUserId(this IHttpContextAccessor context)
        {
            var token = context.HttpContext?.GetToken();
            if (token == null) return null;
            return token.GetClaim("UserId").Value;
        }

        public static async Task<string> GetRequestBodyStringAsync(this HttpRequest request)
        {
            request.EnableBuffering();

            var requestBodyString = await new StreamReader(request.Body).ReadToEndAsync();

            if (request.Headers.Values.Contains("multipart/form-data") ||
                string.IsNullOrWhiteSpace(requestBodyString)) return null;

            // Reset the request body stream position so the next middleware can read it
            request.Body.Position = 0;

            return requestBodyString;
        }
    }
}