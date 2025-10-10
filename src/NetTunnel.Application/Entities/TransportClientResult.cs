using System.Net;

namespace NetTunnel.Application.Entities
{
    public struct TransportClientResult
    {
        public byte[] Data { get; set; }
        public IPEndPoint RemoteEndPoint { get; set; }
    }
}
