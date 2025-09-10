using NetTunnel.Domain.Entities;
using NetTunnel.Domain.Interfaces;

namespace NetTunnel.Infrastucture.Processing
{
    public class DefaultPacketBuilder : ITunnelPacketBuilder<DefaultTunnelPacket>
    {
        public byte[] BuildPacket(DefaultTunnelPacket packet)
        {
            if (packet.Data == null || packet.Data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty");

            if (packet.Sign == null || packet.Sign.Length == 0)
                throw new ArgumentException("Sign cannot be null or empty");

            var rawData = new byte[packet.Data.Length +  packet.Sign.Length];
            var rawDataOffset = 0;

            WriteToRawData(rawData, BitConverter.GetBytes(packet.Sign.Length), ref rawDataOffset);
            WriteToRawData(rawData, packet.Sign, ref rawDataOffset);
            WriteToRawData(rawData, packet.Data, ref rawDataOffset);

            return rawData;
        }

        public DefaultTunnelPacket ParsePacket(byte[] rawData)
        {
            if (rawData.Length < sizeof(int))
                throw new ArgumentException("Packet too short");

            int rawDataOffset = 0;
            
            int signLength = BitConverter.ToInt32(rawData, 0);
            rawDataOffset += sizeof(int);

            if (signLength < 0 || rawDataOffset + signLength > rawData.Length)
                throw new InvalidDataException("Invalid signature length");

            byte[] sign = new byte[signLength];
            ReadFromRawData(sign, rawData, ref rawDataOffset);

            byte[] data = new byte[rawData.Length - rawDataOffset];
            ReadFromRawData(data, rawData, ref rawDataOffset);

            return new DefaultTunnelPacket
            {
                Data = data,
                Sign = sign,
            };
        }

        private void WriteToRawData(byte[] rawData, byte[] data, ref int rawDataOffset)
        {
            Buffer.BlockCopy(data, 0, rawData, rawDataOffset, data.Length);
            rawDataOffset += data.Length;
        }

        private void ReadFromRawData(byte[] rawData, byte[] data, ref int rawDataOffset)
        {
            Buffer.BlockCopy(rawData, rawDataOffset, data, 0, data.Length);
            rawDataOffset += data.Length;
        }
    }
}
