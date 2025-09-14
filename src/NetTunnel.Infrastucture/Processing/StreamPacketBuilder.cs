using NetTunnel.Application.Entities;
using NetTunnel.Application.Interfaces;

namespace NetTunnel.Infrastucture.Processing
{
    public class StreamPacketBuilder : ITunnelPacketBuilder<DefaultTunnelPacket>
    {
        public byte[] BuildPacket(DefaultTunnelPacket packet)
        {
            if (packet.Data == null || packet.Data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty");

            if (packet.Sign == null || packet.Sign.Length == 0)
                throw new ArgumentException("Sign cannot be null or empty");

            var signLengthBytes = BitConverter.GetBytes(packet.Sign.Length);
            var rawData = new byte[signLengthBytes.Length + packet.Data.Length +  packet.Sign.Length];

            using (var stream = new MemoryStream())
            {
                stream.Write(signLengthBytes,0, signLengthBytes.Length);
                stream.Write(packet.Sign,0, packet.Sign.Length);
                stream.Write(packet.Data,0, packet.Data.Length);

                return stream.ToArray();
            }
        }

        public DefaultTunnelPacket ParsePacket(byte[] rawData)
        {
            if (rawData.Length < sizeof(int))
                throw new ArgumentException("Packet too short");

            using (var stream = new MemoryStream(rawData))
            {
                var signLengthBuffer = new byte[4];
                stream.Read(signLengthBuffer, 0, signLengthBuffer.Length);

                int signLength = BitConverter.ToInt32(signLengthBuffer);

                if (signLength < 0 || signLengthBuffer.Length + signLength > rawData.Length)
                    throw new InvalidDataException("Invalid signature length");

                byte[] sign = new byte[signLength];
                stream.Read(sign, 0, sign.Length);

                byte[] data = new byte[rawData.Length - sign.Length - signLengthBuffer.Length];
                stream.Read(data, 0, data.Length);

                return new DefaultTunnelPacket
                {
                    Data = data,
                    Sign = sign
                };
            }
        }
    }
}
