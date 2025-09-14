namespace NetTunnel.Application.Interfaces
{
    public interface ITunnelPacketBuilder<TPacket> where TPacket : class
    {
        byte[] BuildPacket(TPacket packet);
        TPacket ParsePacket(byte[] rawData);
    }
}
