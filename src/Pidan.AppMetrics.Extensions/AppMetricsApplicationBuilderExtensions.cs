using Microsoft.AspNetCore.Builder;

namespace Pidan.AppMetrics.Extensions
{
    public static class AppMetricsApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseAppMetrics(this IApplicationBuilder app)
        {
            app.UseMetricsAllMiddleware();
            app.UseMetricsAllEndpoints();

            return app;
        }
    }
}