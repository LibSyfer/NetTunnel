using System.Net;

namespace NetTunnel.UdpTrafficGenerator
{
    internal class TraficGeneratorSettings
    {
        public const string Section = "TraficGeneratorSettings";
        public string TargetHost { get; set; } = string.Empty;
        public int TargetPort { get; set; }
        public int SendingDelayMs { get; set; }
        public string SendingMessage { get; set; } = string.Empty;
        public IPAddress GetTargetIp =>
            Dns.GetHostEntry(TargetHost).AddressList.FirstOrDefault() ??
            throw new InvalidOperationException($"Could not resolve IP address for host: {TargetHost}");
    }
}
