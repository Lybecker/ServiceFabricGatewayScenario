using AppService.Stateful;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;

namespace Gateway.Service.Controllers
{
    [ServiceRequestActionFilter]
    public class ValuesController : ApiController
    {
        // GET api/values 
        //public IEnumerable<string> Get()
        public async Task<IEnumerable<string>> Get()
        {
            var uri = new ServiceUriBuilder("AppService","Stateful").ToUri();
            IStateful client = ServiceProxy.Create<IStateful>(uri, new ServicePartitionKey(1));

            string message = await client.GetHelloWorld();

            return new[] { message };
            //return new string[] { "value1", "value2" };
        }

        // GET api/values/5 
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values 
        public void Post([FromBody]string value)
        {
        }

        // PUT api/values/5 
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5 
        public void Delete(int id)
        {
        }
    }
}