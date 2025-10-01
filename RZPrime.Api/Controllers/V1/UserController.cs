using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using RZPrime.Services._User;
using RZPrime.Services._User.DTOs.Results;
using RZPrime.Services._User.DTOs.Storages;
using RZPrime.Services._User.DTOs.Updates;
using RZPrime.Utilities.Api;
using RZPrime.Utilities.Attributes;
using RZPrime.Utilities.Filters;
using Swashbuckle.AspNetCore.Annotations;

namespace RZPrime.Api.Controllers.V1
{
    [ApiController]
    [ApiResultFilter]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class UserController(IUserService _userService, JwtBlacklistStorage _blacklist) : ApiBaseController
    {


        [HttpPost("[action]")]
        [CustomRateLimit(maxAttemptsCount: 30)]
        [SwaggerOperation(Summary = "For getting Nonce", Tags = ["Auth"])]
        public NonceResult GetNonce(NonceRequest update)
        {
            return _userService.GetNonce(update, Ip);
        }


        [HttpPost("[action]")]
        [CustomRateLimit(maxAttemptsCount:30)]
        [SwaggerOperation(Summary = "For getting Nonce for app", Tags = ["Auth"])]
        public async Task<NonceResult> GetNonceForApp(AppNonceRequest update)
        {
            return await _userService.GetNonceForApp(update, Ip);
        }


        [HttpPost("[action]")]
        [CustomRateLimit(maxAttemptsCount:30)]
        [SwaggerOperation(Summary = "For getting Nonce for web", Tags = ["Auth"])]
        public async Task<NonceResult> GetNonceForWeb(WebNonceRequest update)
        {
            return await _userService.GetNonceForWeb(update, Ip);
        }


        [HttpPost("[action]")]
        [CustomRateLimit(maxAttemptsCount:30)]
        [SwaggerOperation(Summary = "For activate nonce after scan from device", Tags = ["Auth"])]
        public async Task<bool> ActivateNonceAsync(ActivateNonceRequest update)
        {
            return await _userService.ActivateNonceAsync(update);
        }


        [HttpPost("[action]")]
        [CustomRateLimit(maxAttemptsCount: 35)]
        [SwaggerOperation(Summary = "For getting JWT token", Tags = ["Auth"])]
        public async Task<ActionResult> GetToken(NonceVerification update)
        {
            return await _userService.GetToken(update, Ip);
        }

        [HttpPost("[action]")]
        [CustomRateLimit(maxAttemptsCount: 35)]
        [SwaggerOperation(Summary = "For getting JWT token with pure wallet address ", Tags = ["Auth"])]
        public async Task<ActionResult> GetTokenWithPureWalletAddress(GetTokenWithPureWalletAddress update)
        {
            return await _userService.GetTokenWithPureWalletAddress(update, Ip);
        }


        [HttpPost("logout")]
        [CustomRateLimit(maxAttemptsCount: 40)]
        [SwaggerOperation(Summary = "For logout user", Tags = ["Auth"])]
        [Authorize(RequireActiveUser = false)]
        public IActionResult Logout()
        {
            var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (string.IsNullOrEmpty(token))
                return BadRequest("No token provided");

            var expiry = DateTime.UtcNow.AddMinutes(15);

            _blacklist.AddToken(token, expiry);

            return Ok("Logged out successfully");
        }


        [HttpGet("[action]")]
        [Authorize(RequireActiveUser = false)]
        [CustomRateLimit(maxAttemptsCount:50)]
        [SwaggerOperation(Summary = "For getting user available stages stats", Tags = ["Stats"])]
        public async Task<List<GetUserStatsResult>> GetUserStatsAsync()
        {
            return await _userService.GetUserStatsAsync(PublicKey, WalletAddress);
        }



    }
}
