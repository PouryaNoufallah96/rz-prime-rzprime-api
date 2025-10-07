using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
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
    public class StatusController : ApiBaseController
    {
        [HttpGet("[action]")]
        [CustomRateLimit(maxAttemptsCount: 60)]
        [SwaggerOperation(Summary = "System Status", Tags = ["Status"])]
        public async Task<SystemStatus> GetStatus()
        {
            return new SystemStatus { };
        }


    }

    public class SystemStatus
    {
        public bool Checked { get; set; } = true;  
        public bool SystemHealth { get; set; } = true; 
        public bool SystemActivity { get; set; } = true;
        public string ActivityMessage { get; set; } = "All services is active.";
        public string Message { get; set; } = "Please Use Web app For actions";  
    } 
}
