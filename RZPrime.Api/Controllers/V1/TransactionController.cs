//using _CodeAssistant.Api;
//using _CodeAssistant.Filters;
//using Asp.Versioning;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using RZPrime.Services._TransactionLog;
//using RZPrime.Services._TransactionLog.DTOs.Results;
//using RZPrime.Services._User.DTOs.Updates;
//using RZPrime.Utilities.DTOs;
//using Swashbuckle.AspNetCore.Annotations;

//namespace RZPrime.Api.Controllers.V1
//{

//    [ApiController]
//    [ApiResultFilter]
//    [ApiVersion("1")]
//    [Route("api/v{version:apiVersion}/[controller]")]
//    public class TransactionController(ITransactionLogService _transactionLogService) : ApiBaseController
//    {

//        [HttpPost("[action]")]
//        [Authorize]
//        [SwaggerOperation(Summary = "get user transactions log", Tags = ["Transaction"])]
//        public Task<TransactionListResult> ListTransactionsAsync(Pagination pagination, string walletAddress)
//        {
//            return _transactionLogService.ListTransactionsAsync(pagination, walletAddress);
//        }
//    }
//}
