using System.Net;

namespace NetTunnel.Domain.Entities
{
    public struct TransportClientResult
    {
        public byte[] Data { get; set; }
        public IPEndPoint RemoteEndPoint { get; set; }
    }
}
