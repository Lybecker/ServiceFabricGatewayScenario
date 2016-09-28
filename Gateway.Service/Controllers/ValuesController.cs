using AppService.Stateful;
using Microsoft.ServiceFabric.Http.Client;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Gateway.Service.Controllers
{
    [ServiceRequestActionFilter]
    public class ValuesController : ApiController
    {
        private const int partitionKey = 1;
        private static readonly HttpCommunicationClientFactory communicationFactory;
        private static readonly FabricClient fabricClient;

        static ValuesController()
        {
            fabricClient = new FabricClient();

            communicationFactory = new HttpCommunicationClientFactory(new ServicePartitionResolver(() => fabricClient));
        }

        [HttpGet]
        public async Task<IEnumerable<string>> Remoting()
        {
            var serviceUri = new ServiceUriBuilder("AppService", "Stateful").ToUri();
            IStateful client = ServiceProxy.Create<IStateful>(serviceUri, new ServicePartitionKey(partitionKey));

            string message = await client.GetHelloWorld();

            return new[] { message };
        }

        [HttpGet]
        public async Task<string> Http()
        {
            var serviceUri = new ServiceUriBuilder("AppService", "StatelessWebApi").ToUri();

            ServicePartitionClient<HttpCommunicationClient> partitionClient = new ServicePartitionClient<HttpCommunicationClient>(communicationFactory, serviceUri);

            var result = await partitionClient.InvokeWithRetryAsync(client =>
            {
                HttpWebRequest request = WebRequest.CreateHttp(client.Url + "/api/stateless");
                request.Method = "GET";
                //request.ContentType = "application/json";
                request.KeepAlive = false;
                //request.Timeout = (int)client.OperationTimeout.TotalMilliseconds;
                //request.ReadWriteTimeout = (int)client.ReadWriteTimeout.TotalMilliseconds;

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                using (var reader = new StreamReader(response.GetResponseStream(), ASCIIEncoding.ASCII))
                {
                    return reader.ReadToEndAsync();
                }
            });

            return result;
        }


        // Client from https://github.com/xinyanmsft/SFStartupHttp/tree/TestA
        [HttpGet]
        public async Task<string> HttpXinyan()
        {
            var serviceUri = new ServiceUriBuilder("AppService", "StatelessWebApi").ToUri();

            var client = CreateHttpClient();

            var x = await client.GetStringAsync(serviceUri.ToString() + "/api/stateless");
            //var x = await client.GetStringAsync(serviceUri.ToString().Replace("fabric:","http:/") + "/api/stateless");

            return x;

        }

        private HttpClient CreateHttpClient()
        {
            // TODO: To enable circuit breaker pattern, set proper values in CircuitBreakerHttpMessageHandler constructor.
            // One can further customize the Http client behavior by explicitly creating the HttpClientHandler, or by  
            // adjusting ServicePointManager properties.
            var handler = //new CircuitBreakerHttpMessageHandler(10, TimeSpan.FromSeconds(10),
                            new HttpServiceClientHandler(
                                new HttpServiceClientExceptionHandler(
                                    new HttpServiceClientStatusCodeRetryHandler(
                                        new HttpTraceMessageHandler(null))));
            return new HttpClient(handler);
        }
    }
}