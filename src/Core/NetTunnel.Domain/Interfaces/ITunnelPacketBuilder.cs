namespace NetTunnel.Domain.Interfaces
{
    public interface ITunnelPacketBuilder<TPacket> where TPacket : class
    {
        byte[] BuildPacket(TPacket packet);
        TPacket ParsePacket(byte[] data);
    }
}
