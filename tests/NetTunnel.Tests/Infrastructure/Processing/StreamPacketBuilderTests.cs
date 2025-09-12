using NetTunnel.Application.Entities;
using NetTunnel.Infrastucture.Processing;

namespace NetTunnel.Tests.Infrastructure.Processing
{
    public class StreamPacketBuilderTests
    {
        private readonly StreamPacketBuilder _packetBuilder;

        public StreamPacketBuilderTests()
        {
            _packetBuilder = new StreamPacketBuilder();
        }

        [Fact]
        public void BuildPacket_WithValidPacket_ReturnsCorrectBytyArray()
        {
            // Arrange
            var originalPacket = new DefaultTunnelPacket
            {
                Data = [0x01, 0x02, 0x03],
                Sign = [0xFF, 0xFE]
            };

            // Act
            byte[] result = _packetBuilder.BuildPacket(originalPacket);

            // Assert
            int expectedSignLength = originalPacket.Sign.Length;
            byte[] expectedSignLengthBytes = BitConverter.GetBytes(expectedSignLength);

            Assert.Equal(expectedSignLengthBytes.Length + originalPacket.Sign.Length + originalPacket.Data.Length, result.Length);

            byte[] actualSignLengthBytes = new byte[sizeof(int)];
            Buffer.BlockCopy(result, 0, actualSignLengthBytes, 0, 4);
            Assert.Equal(expectedSignLengthBytes, actualSignLengthBytes);

            byte[] actualSign = new byte[expectedSignLength];
            Buffer.BlockCopy(result, sizeof(int), actualSign, 0, expectedSignLength);
            Assert.Equal(originalPacket.Sign, actualSign);

            byte[] actualData = new byte[originalPacket.Data.Length];
            Buffer.BlockCopy(result, sizeof(int) + expectedSignLength, actualData, 0, originalPacket.Data.Length);
            Assert.Equal(originalPacket.Data, actualData);
        }

        [Theory]
        [InlineData(null, new byte[] { 0x01 })]
        [InlineData(new byte[0], new byte[] { 0x01 })]
        [InlineData(new byte[] { 0x01 }, null)]
        [InlineData(new byte[] { 0x01 }, new byte[0])]
        public void BuildPacket_WithInvalidPacketAgrumets_ThrowsArgumentException(byte[] data, byte[] sign)
        {
            // Arrange
            var invalidPacket = new DefaultTunnelPacket
            {
                Data = data,
                Sign = sign
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _packetBuilder.BuildPacket(invalidPacket));
        }

        [Fact]
        public void ParsePacket_WithValidRawData_ReturnsCorrectPacket()
        {
            // Arrange
            byte[] expectedSign = [0xFF, 0xFE];
            byte[] expectedData = [0x01, 0x02, 0x03];
            byte[] signLengthBytes = BitConverter.GetBytes(expectedSign.Length);

            byte[] rawData = new byte[signLengthBytes.Length + expectedSign.Length + expectedData.Length];
            Buffer.BlockCopy(signLengthBytes, 0, rawData, 0, signLengthBytes.Length);
            Buffer.BlockCopy(expectedSign, 0, rawData, signLengthBytes.Length, expectedSign.Length);
            Buffer.BlockCopy(expectedData, 0, rawData, signLengthBytes.Length + expectedSign.Length, expectedData.Length);

            // Act
            var resultPacket = _packetBuilder.ParsePacket(rawData);

            // Assert
            Assert.NotNull(resultPacket);
            Assert.Equal(expectedSign, resultPacket.Sign);
            Assert.Equal(expectedData, resultPacket.Data);
        }

        [Fact]
        public void ParsePacket_WithTooShoprtData_ThrowsArgumentException()
        {
            // Arrange
            byte[] tooShortRawData = new byte[3];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _packetBuilder.ParsePacket(tooShortRawData));
        }

        [Fact]
        public void ParsePacket_WithInvalidSignLength_ThrowsInvalidDataException()
        {
            // Arrange
            byte[] negativeSignLength = BitConverter.GetBytes(-1);
            byte[] rawData = new byte[10];
            Buffer.BlockCopy(negativeSignLength, 0, rawData, 0, negativeSignLength.Length);

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => _packetBuilder.ParsePacket(rawData));
        }

        [Fact]
        public void ParsePacket_WithSignLengthExceedingData_ThrowsInvalidDataException()
        {
            // Arrange
            byte[] hugeSignLength = BitConverter.GetBytes(1000);
            byte[] rawData = new byte[10];
            Buffer.BlockCopy(hugeSignLength, 0, rawData, 0, hugeSignLength.Length);

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => _packetBuilder.ParsePacket(rawData));
        }

        [Fact]
        public void BuildAndParsePacket_RoundTrip_ReturnsEquivalentPacket()
        {
            // Arrange
            var originalPacket = new DefaultTunnelPacket
            {
                Data = [0x01, 0x02, 0x03, 0x04, 0x05],
                Sign = [0xAA, 0xBB, 0xCC, 0xDD]
            };

            // Act
            byte[] buildData = _packetBuilder.BuildPacket(originalPacket);
            var parsedPacket = _packetBuilder.ParsePacket(buildData);

            // Assert
            Assert.NotNull(parsedPacket);
            Assert.Equal(originalPacket.Sign, parsedPacket.Sign);
            Assert.Equal(originalPacket.Data, parsedPacket.Data);
        }

        [Theory]
        [MemberData(nameof(GetPacketTestData))]
        public void BuildAndParsePacket_WithVariousData_ReturnsEquivalentPacket(byte[] data, byte[] sign)
        {
            // Arrange
            var originalPacket = new DefaultTunnelPacket { Data = data, Sign = sign };

            // Act
            byte[] builtData = _packetBuilder.BuildPacket(originalPacket);
            var parsedPacket = _packetBuilder.ParsePacket(builtData);

            // Assert
            Assert.Equal(originalPacket.Data, parsedPacket.Data);
            Assert.Equal(originalPacket.Sign, parsedPacket.Sign);
        }

        public static IEnumerable<object[]> GetPacketTestData()
        {
            yield return new object[] { new byte[] { 0x01 }, new byte[] { 0x02 } };
            yield return new object[] { new byte[100], new byte[50] };
            yield return new object[] { new byte[] { 0x00 }, new byte[] { 0xFF } };
        }
    }
}
