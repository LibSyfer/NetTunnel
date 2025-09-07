using System.Net;

namespace NetTunnel.UdpTrafficGenerator
{
    internal class TraficGeneratorSettings
    {
        public const string Section = "TraficGeneratorSettings";
        public string TargetIp { get; set; } = string.Empty;
        public int TargetPort { get; set; }
        public int SendingDelayMs { get; set; }
        public string SendingMessage { get; set; } = string.Empty;
        public IPAddress GetTargetIp => IPAddress.Parse(TargetIp);
    }
}
