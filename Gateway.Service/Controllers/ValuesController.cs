using AppService.Stateful;
using Microsoft.ServiceFabric.Http.Client;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.ServiceFabric;

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

        [HttpGet]
        public async Task<string> Tcp()
        {
            string result;
            int attempts = 0;
            int timeoutExceptions = 0;
            DateTime startTime = DateTime.UtcNow;
            try
            {
                var serviceUri = new ServiceUriBuilder("AppService", "StatelessWebApi").ToUri();
                ServicePartitionClient<HttpCommunicationClient> partitionClient = new ServicePartitionClient<HttpCommunicationClient>(communicationFactory, serviceUri);

                result = await partitionClient.InvokeWithRetryAsync(async client =>
                {
                    attempts++;
                    string innerResult;
                    try
                    {
                        innerResult = await HttpRequestUsingTcpClientAsync(client.Url.Host, client.Url.Port, "/api/stateless");
                    }
                    catch (Exception)
                    {
                        timeoutExceptions++;
                        throw;
                    }
                    return innerResult;
                });
            }
            catch (Exception e)
            {
                result = $"Error: {e.Message} - {e.StackTrace}";
            }
            return $"Attempts: {attempts} - TimeoutsInTcp: {timeoutExceptions} - Took: {DateTime.UtcNow.Subtract(startTime).TotalMilliseconds.ToString("0.####")} ms - {result}";
        }

        // Client from https://github.com/xinyanmsft/SFStartupHttp/tree/TestA
        [HttpGet]
        public async Task<string> HttpXinyan()
        {
            string requestUri = new NamedApplication("fabric:/AppService")
                                    .AppendNamedService("StatelessWebApi")
                                    .BuildEndpointUri(endpointName: "")
                                    + "/api/stateless";

            var client = CreateHttpClient();
            var result = await client.GetStringAsync(requestUri);
            return result;
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

        private static async Task<string> HttpRequestUsingTcpClientAsync(string host = "baoanders.westeurope.cloudapp.azure.com", int port = 8127,
            string urlPath = "/api/values/long?sec=5")
        {
            string result;

            using (var tcp = new TcpClient())
            {
                IAsyncResult ar = tcp.BeginConnect(host, port, null, null);
                System.Threading.WaitHandle wh = ar.AsyncWaitHandle;
                try
                {
                    if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1), false))
                    {
                        tcp.Close();
                        throw new TimeoutException("Failed to connect to server");
                    }

                    using (var stream = tcp.GetStream())
                    {
                        //tcp.SendTimeout = 500;
                        //tcp.ReceiveTimeout = 1000;

                        // Send request headers
                        var builder = new StringBuilder();
                        builder.AppendLine($"GET {urlPath} HTTP/1.1");
                        builder.AppendLine($"Host: {host}:{port}");
                        //builder.AppendLine("Content-Length: " + data.Length);   // only for POST request
                        builder.AppendLine("Connection: close");
                        builder.AppendLine();
                        var header = Encoding.ASCII.GetBytes(builder.ToString());
                        await stream.WriteAsync(header, 0, header.Length);

                        // Send payload data if you are POST request
                        //await stream.WriteAsync(data, 0, data.Length);

                        // receive data
                        using (var memory = new MemoryStream())
                        {
                            await stream.CopyToAsync(memory);
                            memory.Position = 0;
                            var data = memory.ToArray();

                            var index = BinaryMatch(data, Encoding.ASCII.GetBytes("\r\n\r\n")) + 4;
                            var headers = Encoding.ASCII.GetString(data, 0, index);
                            memory.Position = index;

                            if (headers.IndexOf("Content-Encoding: gzip") > 0)
                            {
                                using (GZipStream decompressionStream = new GZipStream(memory, CompressionMode.Decompress))
                                using (var decompressedMemory = new MemoryStream())
                                {
                                    decompressionStream.CopyTo(decompressedMemory);
                                    decompressedMemory.Position = 0;
                                    result = Encoding.UTF8.GetString(decompressedMemory.ToArray());
                                }
                            }
                            else
                            {
                                result = Encoding.UTF8.GetString(data, index, data.Length - index);
                            }
                        }
                    } //End TCP.GetStream()
                }
                finally
                {
                    wh.Close();
                }
            }
            return result;
        }

        private static int BinaryMatch(byte[] input, byte[] pattern)
        {
            int sLen = input.Length - pattern.Length + 1;
            for (int i = 0; i < sLen; ++i)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; ++j)
                {
                    if (input[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}