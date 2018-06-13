using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Pidan.Resiliense.Http
{
    public static class ResilientServiceCollectionExtensions
    {
        /// <summary>
        /// 添加ResilientHttpClient
        /// </summary>
        /// <param name="services"></param>
        /// <param name="httpMessageHandler">处理handler</param>
        /// <param name="retryCount">重试次数</param>
        /// <param name="exceptionsAllowedBeforeBreaking">熔断前允许连续异常次数</param>
        /// <returns></returns>
        public static IServiceCollection AddResilientHttpClient(this IServiceCollection services,
            HttpMessageHandler httpMessageHandler = null, int retryCount = 5, int exceptionsAllowedBeforeBreaking = 5)
        {
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services
                .AddSingleton(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<ResilientHttpClient>>();
                    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();

                    return new ResilientHttpClientFactory(logger, httpContextAccessor,
                        exceptionsAllowedBeforeBreaking, retryCount, httpMessageHandler);
                });

            services
                .AddSingleton<IHttpClient, ResilientHttpClient>(sp =>
                    sp.GetRequiredService<ResilientHttpClientFactory>().CreateResilientHttpClient());

            return services;
        }
    }
}