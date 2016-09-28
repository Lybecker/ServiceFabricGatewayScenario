using System.Threading;

namespace Microsoft.ServiceFabric.Http
{
    public static class ServiceFabricDiagnostics
    {
        private static string RequestCorrelationId;
        private static string RequestOrigin;

        public const string CorrelationHeaderName = "__CorrelationId";
        public const string RequestOriginHeaderName = "__RequestOrigin";

        public static string GetRequestCorrelationId()
        {
            return RequestCorrelationId;
        }

        public static void SetRequestCorrelationId(string value)
        {
            RequestCorrelationId = value;
        }

        public static string GetRequestOrigin()
        {
            return RequestOrigin;
        }

        public static void SetRequestOrigin(string value)
        {
            RequestOrigin = value;
        }
    }
}
