using RZPrime.Services._User.DTOs.Storages;

namespace RZPrime.Api.Utilities.Middlewares
{
    public class JwtBlacklistMiddleware
    {
        private readonly RequestDelegate _next;

        public JwtBlacklistMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, JwtBlacklistStorage blacklist)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (!string.IsNullOrEmpty(token) && blacklist.IsTokenBlacklisted(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                //await context.Response.WriteAsync("Token is blacklisted. Please login again.");
                throw new RZPrime.Utilities.Exceptions.AuthorizationException("Please login again.");
            }

            await _next(context);
        }
    }
}
