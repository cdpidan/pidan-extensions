using System;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Pidan.Resiliense.Http;
using Polly;

namespace Pidan.Resiliense.Http
{
    public class ResilientHttpClientFactory
    {
        private readonly ILogger<ResilientHttpClient> _logger;
        private readonly int _retryCount;
        private readonly int _exceptionsAllowedBeforeBreaking;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ResilientHttpClientFactory(ILogger<ResilientHttpClient> logger, IHttpContextAccessor httpContextAccessor,
            int exceptionsAllowedBeforeBreaking = 5, int retryCount = 5)
        {
            _logger = logger;
            _exceptionsAllowedBeforeBreaking = exceptionsAllowedBeforeBreaking;
            _retryCount = retryCount;
            _httpContextAccessor = httpContextAccessor;
        }

        public ResilientHttpClient CreateResilientHttpClient()
            => new ResilientHttpClient(origin => CreatePolicies(), _logger, _httpContextAccessor);

        private Policy[] CreatePolicies()
        {
            return new Policy[]
            {
                //  重试策略
                Policy.Handle<HttpRequestException>()
                    .WaitAndRetryAsync(
                        //  重试次数
                        _retryCount,

                        // exponential backofff
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),

                        // on retry
                        (exception, timeSpan, retryCount, context) =>
                        {
                            var msg = $"Retry {retryCount} implemented with Polly's RetryPolicy " +
                                      $"of {context.PolicyKey} " +
                                      $"at {context.OperationKey}, " +
                                      $"due to: {exception}.";
                            _logger.LogWarning(msg);
                            _logger.LogDebug(msg);
                        }
                    ),

                // 熔断策略
                Policy.Handle<HttpRequestException>()
                    .CircuitBreakerAsync(
                        // 断路前允许的错误异常次数
                        _exceptionsAllowedBeforeBreaking,

                        // 断路时间
                        TimeSpan.FromMinutes(1),
                        (exception, duration) =>
                        {
                            //断路器打开
                            _logger.LogTrace("熔断器开启");
                        },
                        () =>
                        {
                            //断路器关闭
                            _logger.LogTrace("熔断器关闭");
                        }
                    )
            };
        }
    }
}