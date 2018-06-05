namespace Pidan.Consul.Extensions.Options
{
    public class ServiceDiscoveryOptions
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// 检查间隔，单位：秒
        /// </summary>
        public int CheckInterval { get; set; } = 10;

        /// <summary>
        /// 服务检测失败后，多久进行反注册（取消注册），单位：秒
        /// </summary>
        public int DeregisterCriticalServiceAfter { get; set; } = 30;

        public ConsulOptions Consul { get; set; }
    }
}