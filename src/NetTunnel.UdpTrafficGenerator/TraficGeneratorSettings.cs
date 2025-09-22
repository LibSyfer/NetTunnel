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

        public IPAddress GetListenIp => IPAddress.Parse(ListenIp);
        public IPAddress GetTargetIp => GetIpAddressFromString(TargetHost);

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
