using Microsoft.AspNetCore.Mvc;
using RZPrime.Domain.Collections;
using RZPrime.Services._User.DTOs.Results;
using RZPrime.Services._User.DTOs.Updates;

namespace RZPrime.Services._User
{
    public interface IUserService
    {

        //auth 
        Task<NonceResult> GetNonceForApp(AppNonceRequest update, string ip);
        Task<NonceResult> GetNonceForWeb(WebNonceRequest update, string ip);
        Task<bool> ActivateNonceAsync(ActivateNonceRequest update);
        NonceResult GetNonce(NonceRequest update, string ip);
        Task<ActionResult> GetToken(NonceVerification update, string ip);
        Task<ActionResult> GetTokenWithPureWalletAddress(GetTokenWithPureWalletAddress update, string ip);

        //stats
        Task<List<GetUserStatsResult>> GetUserStatsAsync(string userPublicKey, string walletAddress);
    }
}
