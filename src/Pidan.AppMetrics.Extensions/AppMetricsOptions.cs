namespace Pidan.AppMetrics.Extensions
{
    public class AppMetricsOptions
    {
        public string DataBaseName { get; set; }

        public string ConnectionString { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string App { get; set; }

        public string Env { get; set; }
    }
}