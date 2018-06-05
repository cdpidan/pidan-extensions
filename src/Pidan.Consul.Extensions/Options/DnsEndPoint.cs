using System.Net;

namespace Pidan.Consul.Extensions.Options
{
    public class DnsEndPoint
    {
        public string Address { get; set; }

        public int Port { get; set; }

        public IPEndPoint ToIPEndPoint()
        {
            return new IPEndPoint(IPAddress.Parse(Address), Port);
        }
    }
}