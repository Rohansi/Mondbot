using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MondBot.Master.Models;

namespace MondBot.Master.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RunController : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<RunCodeResponse>> Get([FromBody] RunCodeRequest request)
        {
            var (imageData, output) = await Common.RunScript(request.Code);

            return new RunCodeResponse
            {
                Output = output,
                Image = imageData != null ? Convert.ToBase64String(imageData) : null,
            };
        }
    }
}
