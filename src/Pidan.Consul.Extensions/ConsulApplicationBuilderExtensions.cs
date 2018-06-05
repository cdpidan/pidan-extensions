using System;
using System.Linq;
using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pidan.Consul.Extensions.Options;

namespace Pidan.Consul.Extensions
{
    public static class ConsulApplicationBuilderExtensions
    {
        /// <summary>
        /// 使用Consul 服务发现
        /// </summary>
        /// <param name="app"></param>
        /// <param name="healthCheckUrl">相对路径</param>
        /// <returns></returns>
        public static IApplicationBuilder UseConsulDiscoveryClient(this IApplicationBuilder app,
            string healthCheckUrl = null)
        {
            var logger = app.ApplicationServices.GetRequiredService<ILogger<IConsulClient>>();
            var applicationLifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
            var consul = app.ApplicationServices.GetRequiredService<IConsulClient>();
            var serviceOptions =
                app.ApplicationServices.GetRequiredService<IOptions<ServiceDiscoveryOptions>>();

            applicationLifetime.ApplicationStarted.Register(() =>
                Register(app, consul, serviceOptions, logger, healthCheckUrl));
            applicationLifetime.ApplicationStopped.Register(() => DeRegister(app, consul, serviceOptions));

            if (string.IsNullOrWhiteSpace(healthCheckUrl))
                app.UseHealthCheck();

            return app;
        }

        private static void Register(IApplicationBuilder app, IConsulClient consul,
            IOptions<ServiceDiscoveryOptions> serviceOptions, ILogger logger, string healthCheckUrl)
        {
            if (string.IsNullOrWhiteSpace(serviceOptions.Value.ServiceName))
                return;

            var features = app.Properties["server.Features"] as FeatureCollection;
            if (features == null)
                return;

            var addresses = features.Get<IServerAddressesFeature>()
                .Addresses
                .Select(p => new Uri(p))
                .ToList();

            if (!addresses.Any())
                return;

            healthCheckUrl = string.IsNullOrWhiteSpace(healthCheckUrl) ? "consul/health-check" : healthCheckUrl;

            foreach (var address in addresses)
            {
                var serviceId = $"{serviceOptions.Value.ServiceName}_{address.Host}:{address.Port}";

                var httpCheck = new AgentServiceCheck
                {
                    DeregisterCriticalServiceAfter =
                        TimeSpan.FromSeconds(serviceOptions.Value.DeregisterCriticalServiceAfter),
                    Interval = TimeSpan.FromSeconds(serviceOptions.Value.CheckInterval),
                    HTTP = new Uri(address, healthCheckUrl).OriginalString
                };

                var registration = new AgentServiceRegistration
                {
                    ID = serviceId,
                    Address = address.Host,
                    Port = address.Port,
                    Checks = new[] {httpCheck},
                    Name = serviceOptions.Value.ServiceName
                };

                consul.Agent.ServiceRegister(registration).GetAwaiter().GetResult();

                logger.LogInformation($"添加服务成功, 服务名称：[{serviceOptions.Value.ServiceName}], 健康检查地址[{httpCheck.HTTP}]");
            }
        }

        private static void DeRegister(IApplicationBuilder app, IConsulClient consul,
            IOptions<ServiceDiscoveryOptions> serviceOptions)
        {
            var features = app.Properties["server.Features"] as FeatureCollection;
            if (features == null)
                return;

            var addresses = features.Get<IServerAddressesFeature>()
                .Addresses
                .Select(p => new Uri(p));

            foreach (var address in addresses)
            {
                var serviceId = $"{serviceOptions.Value.ServiceName}_{address.Host}:{address.Port}";

                consul.Agent.ServiceDeregister(serviceId).GetAwaiter().GetResult();
            }
        }
    }
}