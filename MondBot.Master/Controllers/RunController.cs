using Microsoft.AspNetCore.Mvc;

namespace MondBot.Master.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RunController
    {
        [HttpGet]
        public ActionResult<string> Get()
        {
            return "hello";
        }
    }
}
