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
            IConfiguration configuration)
        {
            //注册Consul 配置
            services.Configure<ServiceDiscoveryOptions>(configuration);

            services.AddSingleton<IConsulClient>(p => new ConsulClient(cfg =>
            {
                var serviceDiscoveryOptions = p.GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;

                if (!string.IsNullOrWhiteSpace(serviceDiscoveryOptions.Consul.HttpEndPoint))
                {
                    // if not configured, the client will use the default value "127.0.0.1:8500"
                    cfg.Address = new Uri(serviceDiscoveryOptions.Consul.HttpEndPoint);
                }
            }));
            
            services.AddSingleton<IDnsQuery>(p =>
            {
                var serviceDiscoveryOptions = p.GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;
                return new LookupClient(serviceDiscoveryOptions.Consul.DnsEndPoint.ToIPEndPoint());
            });
            
            return services;
        }
    }
}