using System.Net;

namespace NetTunnel.Client
{
    public class ClientSettings
    {
        public const string Section = "ClientSettings";
        public int ListenPort { get; set; }
        public string ServerIp { get; set; } = string.Empty;
        public int ServerPort { get; set; }
        public string PreSharedKey { get; set; } = string.Empty;
        public IPAddress GetServerIp => IPAddress.Parse(ServerIp);
    }
}
