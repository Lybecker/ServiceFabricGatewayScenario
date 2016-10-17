using AppService.Stateful;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Gateway.Service.Controllers
{
    [ServiceRequestActionFilter]
    public class ValuesController : ApiController
    {
        private const int partitionKey = 1;
        private static readonly HttpCommunicationClientFactory communicationFactory;
        private const int MaxQueryRetryCount = 3;
        private static readonly TimeSpan backoffQueryDelay;
        private static readonly FabricClient fabricClient;

        static ValuesController()
        {
            backoffQueryDelay = TimeSpan.FromSeconds(3);

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
        public async Task<string> Wcf()
        {
            var serviceUri = new ServiceUriBuilder("AppService", "Stateful").ToUri();
            
            //IStateful client = WcfServiceClientBuilder.CreateClient(serviceUri, new ServicePartitionKey(partitionKey));
            //string message = await client.GetHelloWorld();

            var client = WcfServiceClientBuilder.CreateClient(serviceUri, new ServicePartitionKey(partitionKey));
            var message = await client.InvokeWithRetryAsync(proxy => proxy.Channel.GetHelloWorld());

            return message;
        }


        [HttpGet]
        public string Sleep(int sec)
        {
            System.Threading.Thread.Sleep(sec * 1000);

            return $"Hello World after {sec} s";
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
    }
}