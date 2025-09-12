using RZPrime.Services._Log.DTOs.Updates;
using RZPrime.Services._Log;
using System.Text;
using System.Text.Json;
using RZPrime.Utilities.Services.Contracts;
using RZPrime.Utilities.Enums;
using RZPrime.Utilities.Utilities;
using RZPrime.Utilities.Extension;

namespace RZPrime.Api.Utilities.Middlewares
{
    public class RequestLoggingMiddleware(RequestDelegate _next, ILogService _logService, IJwtService _jwtService)
    {
        public async Task InvokeAsync(HttpContext context)
        {

            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                await _next(context);
                return;
            }

            context.Request.EnableBuffering();

            using var reader = new StreamReader(
                context.Request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            var headers = JsonSerializer.Serialize(context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
            var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;

            var update = new RequestLogUpdate
            {
                ControllerName = context.Request.Path.HasValue ? context.Request.Path.Value.Split("/")[3] : null,
                ApiName = context.Request.Path.HasValue ? context.Request.Path.Value.Split("/")[4] : null,
                Body = body,
                Headers = headers,
                Query = string.IsNullOrWhiteSpace(query) ? null : query,
                RoutePath = context.Request.Path,
                ClientIP = context.GetRequestIpv4()?.Split(",")[0]
            };

            string publicKey = "anonymous";
            string walletAddress = "anonymous";

            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (!string.IsNullOrWhiteSpace(token))
            {
                try
                {
                    var jwtToken = _jwtService.Validate(token);
                    if (jwtToken != null)
                    {
                        publicKey = jwtToken.Claims.FirstOrDefault(c => c.Type == Claims.PublicKey.ToDisplay())?.Value ?? "anonymous";
                        walletAddress = jwtToken.Claims.FirstOrDefault(c => c.Type == Claims.WalletAddress.ToDisplay())?.Value ?? "anonymous";
                    }
                }
                catch
                {
                }
            }
            await _logService.CaptureRequestLogAsync(update, publicKey, walletAddress);

            await _next(context);
        }
    }
}
