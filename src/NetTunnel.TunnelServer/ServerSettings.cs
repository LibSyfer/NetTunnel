using System.Net;

namespace NetTunnel.TunnelServer
{
    internal class ServerSettings
    {
        public const string Section = "TunnelServerSettings";
        public string ListenIp { get; set; } = string.Empty;
        public int ListenPort { get; set; }
        public string TargetIp { get; set; } = string.Empty;
        public int TargetPort { get; set; }
        public string PreSharedKey { get; set; } = string.Empty;
        public IPAddress GetListenIp => IPAddress.Parse(ListenIp);
        public IPAddress GetTargetIp => IPAddress.Parse(TargetIp);
    }
}
