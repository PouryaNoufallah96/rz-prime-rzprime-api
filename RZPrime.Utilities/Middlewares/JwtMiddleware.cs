using Microsoft.AspNetCore.Http;
using RZPrime.Utilities.Services.Contracts;

namespace RZPrime.Utilities.Middlewares
{
    public class JwtMiddleware(RequestDelegate next, IJwtService jwtService)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (!string.IsNullOrEmpty(token))
            {
                var jwtToken = jwtService.Validate(token);
                context.Items["Token"] = jwtToken;
            }

            await next(context);
        }
    }
}
