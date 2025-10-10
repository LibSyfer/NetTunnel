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
        public IPAddress GetExternalTargetIp => GetIpAddressFromString(ExternalTargetHost);
        public IPAddress GetTunnelListenIp => IPAddress.Parse(TunnelListenIp);
        public IPAddress GetTunnelTargetIp => GetIpAddressFromString(TunnelTargetHost);

        public static IPAddress GetIpAddressFromString(string address)
        {
            if (address == null)
                throw new ArgumentNullException("Address string cannot be null");

            if (IPAddress.TryParse(address, out IPAddress? ipAddress))
                return ipAddress;

            return Dns.GetHostEntry(address).AddressList.FirstOrDefault() ??
                throw new InvalidOperationException($"Cannot resolve address: {address}");
        }
    }
}
