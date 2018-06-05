using System.Linq;
using DnsClient;

namespace Pidan.Consul.Extensions
{
    public static class ConsulDnsClientExtensions
    {
        public static string ResolveDefaultServiceUrl(this IDnsQuery dnsQuery, string serviceName, string scheme = "http")
        {
            var addresses = dnsQuery.ResolveService("service.consul", "user_api");
            var address = addresses.FirstOrDefault();
            if (address == null)
            {
                return null;
            }

            var host = address.AddressList.Any()
                ? address.AddressList.FirstOrDefault()?.ToString()
                : address.HostName;
            var port = address.Port;
            return $"{scheme}://{host}:{port}";
        }
    }
}