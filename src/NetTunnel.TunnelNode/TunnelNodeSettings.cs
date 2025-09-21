using System.Net;

namespace NetTunnel.TunnelNode
{
    internal class TunnelNodeSettings
    {
        public const string Section = "TunnelNodeSettings";
        public string ExternalListenIp { get; set; } = string.Empty;
        public int ExternalListenPort { get; set; }
        public string ExternalTargetHost { get; set; } = string.Empty;
        public int ExternalTargetPort { get; set; }

        public string TunnelListenIp { get; set; } = string.Empty;
        public int TunnelListenPort { get; set; }
        public string TunnelTargetHost { get; set; } = string.Empty;
        public int TunnelTargetPort { get; set; }

        public IPAddress GetExternalListenIp => IPAddress.Parse(ExternalListenIp);
        public IPAddress GetExternalTargetIp =>
            Dns.GetHostEntry(ExternalTargetHost).AddressList.FirstOrDefault() ??
            throw new InvalidOperationException($"Could not resolve IP address for host: {ExternalTargetHost}");
        public IPAddress GetTunnelListenIp => IPAddress.Parse(TunnelListenIp);
        public IPAddress GetTunnelTargetIp =>
            Dns.GetHostEntry(TunnelTargetHost).AddressList.FirstOrDefault() ??
            throw new InvalidOperationException($"Could not resolve IP address for host: {TunnelTargetHost}");
    }
}
