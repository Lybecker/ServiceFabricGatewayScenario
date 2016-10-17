using AppService.Stateful;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using System;
using System.Fabric;
using System.ServiceModel;

namespace Gateway.Service
{
    public static class WcfServiceClientBuilder
    {
        public static WcfServiceClient CreateClient(Uri serviceUri, ServicePartitionKey partitionKey = null)
        {
            ServicePartitionResolver serviceResolver = ServicePartitionResolver.GetDefault();

            var binding = WcfUtility.CreateTcpClientBinding();

            //binding.OpenTimeout = TimeSpan.FromSeconds(2);

            return new WcfServiceClient(
                new WcfCommunicationClientFactory<IStateful>(clientBinding: binding, servicePartitionResolver: serviceResolver),
                serviceUri,
                partitionKey);
        }

        private static NetTcpBinding CreateClientConnectionBinding()
        {
            NetTcpBinding binding = new NetTcpBinding(SecurityMode.None)
            {
                OpenTimeout = TimeSpan.FromSeconds(2),
            };

            return binding;
        }
    }
}
