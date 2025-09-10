namespace NetTunnel.Domain.Entities
{
    public class DefaultTunnelPacket
    {
        public byte[]? Data { get; set; }

        public byte[]? Sign { get; set; }
    }
}
