using Microsoft.AspNetCore.Mvc;
using RZPrime.Utilities.Enums;
using RZPrime.Utilities.Extension;
using RZPrime.Utilities.Utilities;
using System.IdentityModel.Tokens.Jwt;

namespace RZPrime.Utilities.Api
{
    public class ApiBaseController : ControllerBase
    {
        protected virtual JwtSecurityToken JwtToken => (JwtSecurityToken)HttpContext.Items["Token"];
        protected virtual string PublicKey => HttpContext.GetClaim(Claims.PublicKey.ToDisplay());
        protected virtual string WalletAddress => HttpContext.GetClaim(Claims.WalletAddress.ToDisplay());
        protected virtual string Language => HttpContext.Request.Headers["Accept-Language"];
        protected virtual string Nonce => HttpContext.Request.Headers["DecryptedNonce"].ToString();
        protected virtual string Ip => HttpContext.GetRequestIpv4();

    }
}
