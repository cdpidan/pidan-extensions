using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Pidan.Consul.Extensions
{
    internal static class ConsulHandleHealthCheckExtensions
    {
        internal static IApplicationBuilder UseHealthCheck(this IApplicationBuilder app)
        {
            app.Map("/consul/health-check", builder =>
            {
                builder.Run(async context =>
                {
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync("{\"success\":true}");
                });
            });

            return app;
        }
    }
}