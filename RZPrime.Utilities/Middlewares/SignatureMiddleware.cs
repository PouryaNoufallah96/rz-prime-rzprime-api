using Microsoft.AspNetCore.Http;
using System.Text;
using RZPrime.Utilities.Services.Contracts;
using RZPrime.Utilities.Models.Settings;
using RZPrime.Utilities.Utilities;
using RZPrime.Utilities.Exceptions.Common;


namespace RZPrime.Utilities.Middlewares
{
    public class SignatureMiddleware(RequestDelegate _next, ISignatureService _signatureService,
        INonceService _nonceService, ApplicationPoolSettings _applicationPool)
    {




        public async Task InvokeAsync(HttpContext context)
        {

            if (context.Request.Path.StartsWithSegments("/hubs/inventory") ||
             context.Request.Path.StartsWithSegments("/hubs/nonceNotify") ||
             context.Request.Path.StartsWithSegments("/hubs/paidorder"))
            {
                await _next(context);
                return;
            }

            var headers = context.Request.Headers;

            var applicationId = headers["ApplicationId"].FirstOrDefault();
            var nonce = headers["Nonce"].FirstOrDefault();
            var signature = headers["Signature"].FirstOrDefault();

            var application = _applicationPool.Applications
                .FirstOrDefault(q => q.ApplicationId == applicationId);


            if (application == null)
                throw new BadRequestException("Invalid Application");

            if (string.IsNullOrEmpty(signature))
                throw new BadRequestException("Signature is not specified");

            bool isMaster = signature == application.MasterSignature;

            if (!isMaster && string.IsNullOrEmpty(nonce))
                throw new BadRequestException("Nonce is not specified");


            if (!isMaster)
            {
                if (!_nonceService.TryUse(nonce, TimeSpan.FromMinutes(5)))
                    throw new BadRequestException("Duplicate nonce");

                byte[] signatureBytes = Convert.FromBase64String(signature);

                bool isValidSignature = _signatureService.Verify(
                    Encoding.UTF8.GetBytes(application.PreSharedKey),
                    Encoding.UTF8.GetBytes(nonce ?? string.Empty),
                    signatureBytes
                );

                if (!isValidSignature)
                    throw new BadRequestException("Signature is not valid");
            }

            await _next(context);
        }


        //public async Task InvokeAsync(HttpContext context)
        //{

        //    if (context.Request.Path.StartsWithSegments("/hubs/inventory") ||
        //        context.Request.Path.StartsWithSegments("/hubs/nonceNotify") ||
        //        context.Request.Path.StartsWithSegments("/hubs/paidorder"))
        //    {
        //        await _next(context);
        //        return;
        //    }


        //    var applicationId = context.Request.Headers["ApplicationId"].FirstOrDefault();
        //    var nonce = context.Request.Headers["Nonce"].FirstOrDefault();
        //    var signature = context.Request.Headers["Signature"].FirstOrDefault();

        //    var application = _applicationPool.Applications.FirstOrDefault(q => q.ApplicationId == applicationId)
        //        .CheckNotNull("Application not found");

        //    if (string.IsNullOrEmpty(nonce) && signature != application.MasterSignature)
        //        throw new BadRequestException("Nonce is not specified");

        //    if (string.IsNullOrEmpty(signature))
        //        throw new BadRequestException("Signature is not specified");

        //    if (_nonceService.Contains(nonce) && signature != application.MasterSignature)
        //        throw new BadRequestException("Invalid nonce!");

        //    if (signature != application.MasterSignature)
        //    {
        //        byte[] signatureBytes = Convert.FromBase64String(signature);

        //        bool verification = _signatureService.Verify(Encoding.UTF8.GetBytes(application.PreSharedKey),
        //                           Encoding.UTF8.GetBytes(nonce ?? string.Empty), signatureBytes);

        //        if (!verification)
        //            throw new BadRequestException("Signature is not valid");

        //        _nonceService.Add(nonce);
        //    }

        //    await _next(context);
        //}
    }
}