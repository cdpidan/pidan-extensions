using System;
using Consul;
using DnsClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pidan.Consul.Extensions.Options;

namespace Pidan.Consul.Extensions
{
    public static class ConsulServiceCollectionExtensions
    {
        public static IServiceCollection AddConsulDiscoveryClient(this IServiceCollection services,
            Action<ServiceDiscoveryOptions> action)
        {
            services.Configure(action);
            
            var options = new ServiceDiscoveryOptions();

            action(options);

            if (options.Consul == null)
                return services;

            if (!string.IsNullOrWhiteSpace(options.Consul.HttpEndPoint))
                services.AddSingleton<IConsulClient>(p => new ConsulClient(cfg =>
                {
                    cfg.Address = new Uri(options.Consul.HttpEndPoint);
                }));

            if (options.Consul.DnsEndPoint != null)
                services.AddSingleton<IDnsQuery>(p => new LookupClient(options.Consul.DnsEndPoint.ToIPEndPoint()));

            return services;
        }
    }
}