using System.Net;

namespace NetTunnel.UdpEchoServer
{
    internal class UdpEchoServerSettings
    {
        public const string Section = "UdpEchoServerSettings";
        public string ListenIp { get; set; } = string.Empty;
        public int ListenPort { get; set; }
        public IPAddress GetListenIp => IPAddress.Parse(ListenIp);
    }
}
