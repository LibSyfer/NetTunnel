using System.Net;

namespace NetTunnel.Client
{
    public class ClientSettings
    {
        public const string Section = "ClientSettings";
        public int ListenPort { get; set; }

        public string ServerIp { get; set; }

        public int ServerPort { get; set; }

        public string PreSharedKey { get; set; }

        public IPAddress GetServerIp => IPAddress.Parse(ServerIp);
    }
}
