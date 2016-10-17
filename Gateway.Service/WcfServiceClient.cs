using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using System;
using System.Threading.Tasks;
using AppService.Stateful;
using Microsoft.ServiceFabric.Services.Client;

namespace Gateway.Service
{
    public class WcfServiceClient : ServicePartitionClient<WcfCommunicationClient<IStateful>>, IStateful
    {
        public WcfServiceClient(WcfCommunicationClientFactory<IStateful> clientFactory, Uri serviceUri, ServicePartitionKey partitionKey = null)
            : base(clientFactory, serviceUri: serviceUri, partitionKey: partitionKey)
        {
        }

        public Task<string> GetHelloWorld()
        {
            return this.InvokeWithRetryAsync(
                client => client.Channel.GetHelloWorld());
        }
    }
}