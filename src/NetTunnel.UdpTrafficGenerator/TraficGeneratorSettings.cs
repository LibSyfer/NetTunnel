using System.Net;

namespace NetTunnel.UdpTrafficGenerator
{
    internal class TraficGeneratorSettings
    {
        public const string Section = "TraficGeneratorSettings";
        public string ListenIp { get; set; } = string.Empty;
        public int ListenPort { get; set; }
        public string TargetHost { get; set; } = string.Empty;
        public int TargetPort { get; set; }
        public int SendingDelayMs { get; set; }
        public string SendingMessage { get; set; } = string.Empty;

        public IPAddress GetListenIpp => IPAddress.Parse(ListenIp);
        public IPAddress GetTargetIp =>
            Dns.GetHostEntry(TargetHost).AddressList.FirstOrDefault() ??
            throw new InvalidOperationException($"Could not resolve IP address for host: {TargetHost}");
    }
}
