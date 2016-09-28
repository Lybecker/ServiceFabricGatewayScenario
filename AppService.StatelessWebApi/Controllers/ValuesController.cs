using System.Collections.Generic;
using System.Web.Http;

namespace AppService.StatelessWebApi.Controllers
{
    [ServiceRequestActionFilter]
    public class StatelessController : ApiController
    {
        // GET api/values 
        public IEnumerable<string> Get()
        {
            return new string[] { "Hello World from stateless service using http"};
        }
    }
}