using NetTunnel.Infrastucture.Security;

namespace NetTunnel.Tests.Infrastructure.Security
{
    public class XorDataObfuscatorTests
    {
        [Fact]
        public void Constructor_WithNullKey_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new XorDataObfuscator(null));
        }

        [Fact]
        public void Constructor_WithEmptyKey_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new XorDataObfuscator(Array.Empty<byte>()));
        }

        [Fact]
        public void ObfuscateAndDeobfuscate_WithSingleByteKey_ReturnsOriginalData()
        {
            // Arrange
            byte[] key = [0xAA];
            byte[] originalData = [0x01, 0x02, 0x03, 0x04];
            var obfuscator = new XorDataObfuscator(key);

            // Act
            byte[] obfuscated = obfuscator.Obfuscate(originalData);
            byte[] deobfuscated = obfuscator.Deobfuscate(obfuscated);

            // Assert
            Assert.Equal(originalData, deobfuscated);
            Assert.NotEqual(originalData, obfuscated);
        }

        [Fact]
        public void ObfuscateAndDeobfuscate_WithMultiByteKey_ReturnsOriginalData()
        {
            // Arrange
            byte[] key = [0xAA, 0xBB, 0xCC];
            byte[] originalData = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06];
            var obfuscator = new XorDataObfuscator(key);

            // Act
            byte[] obfuscated = obfuscator.Obfuscate(originalData);
            byte[] deobfuscated = obfuscator.Deobfuscate(obfuscated);

            // Assert
            Assert.Equal(originalData, deobfuscated);
        }

        [Fact]
        public void ObfuscateAndDeobfuscate_WithDataLargerThanKey_CyclesKeyCorrectly()
        {
            // Arrange
            byte[] key = [0x01, 0x02];
            byte[] originalData = [0x10, 0x20, 0x30, 0x40, 0x50];
            var obfuscator = new XorDataObfuscator(key);

            // Act
            byte[] obfuscated = obfuscator.Obfuscate(originalData);

            // Assert
            // data[0] ^ key[0] = 0x10 ^ 0x01 = 0x11
            // data[1] ^ key[1] = 0x20 ^ 0x02 = 0x22
            // data[2] ^ key[0] = 0x30 ^ 0x01 = 0x31
            // data[3] ^ key[1] = 0x40 ^ 0x02 = 0x42
            // data[4] ^ key[0] = 0x50 ^ 0x01 = 0x51
            byte[] expectedObfuscated = { 0x11, 0x22, 0x31, 0x42, 0x51 };
            Assert.Equal(expectedObfuscated, obfuscated);

            // Verify round-trip
            byte[] deobfuscated = obfuscator.Deobfuscate(obfuscated);
            Assert.Equal(originalData, deobfuscated);
        }

        [Fact]
        public void Obfuscate_WithNullData_ThrowsArgumentException()
        {
            // Arrange
            var obfuscator = new XorDataObfuscator([0x01]);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => obfuscator.Obfuscate(null));
        }

        [Fact]
        public void Obfuscate_WithEmptyData_ThrowsArgumentException()
        {
            // Arrange
            var obfuscator = new XorDataObfuscator([0x01]);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => obfuscator.Obfuscate(Array.Empty<byte>()));
        }

        [Fact]
        public void Deobfuscate_WithNullData_ThrowsArgumentException()
        {
            // Arrange
            var obfuscator = new XorDataObfuscator([0x01]);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => obfuscator.Deobfuscate(null));
        }

        [Fact]
        public void Deobfuscate_WithEmptyData_ThrowsArgumentException()
        {
            // Arrange
            var obfuscator = new XorDataObfuscator([0x01]);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => obfuscator.Deobfuscate(Array.Empty<byte>()));
        }

        [Fact]
        public void Obfuscate_Twice_ReturnsOriginalData()
        {
            // Arrange
            byte[] key = [0x55];
            byte[] originalData = [0xAA, 0xBB, 0xCC];
            var obfuscator = new XorDataObfuscator(key);

            // Act
            byte[] once = obfuscator.Obfuscate(originalData);
            byte[] twice = obfuscator.Obfuscate(once);

            // Assert
            Assert.Equal(originalData, twice);
        }

        [Fact]
        public void Obfuscate_WithZeroKey_ReturnsOriginalData()
        {
            // Arrange
            byte[] key = [0x00];
            byte[] originalData = [0x01, 0x02, 0x03];
            var obfuscator = new XorDataObfuscator(key);

            // Act
            byte[] result = obfuscator.Obfuscate(originalData);

            // Assert
            Assert.Equal(originalData, result);
        }

        [Fact]
        public void Obfuscate_WithAllOnesKey_InvertsAllBits()
        {
            // Arrange
            byte[] key = [0xFF];
            byte[] originalData = [0x00, 0x55, 0xAA, 0xFF];
            var obfuscator = new XorDataObfuscator(key);
            byte[] expected = [0xFF, 0xAA, 0x55, 0x00];

            // Act
            byte[] result = obfuscator.Obfuscate(originalData);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Obfuscate_WithSingleByteData_WorksCorrectly()
        {
            // Arrange
            byte[] key = [0xAA];
            byte[] singleByte = [0x55];
            var obfuscator = new XorDataObfuscator(key);

            // Act & Assert
            byte[] obfuscated = obfuscator.Obfuscate(singleByte);
            byte[] deobfuscated = obfuscator.Deobfuscate(obfuscated);
            Assert.Equal(singleByte, deobfuscated);
            Assert.Equal([0xFF], obfuscated); // 0x55 ^ 0xAA = 0xFF
        }

        [Fact]
        public void Obfuscate_WithLargeData_WorksCorrectly()
        {
            // Arrange
            byte[] key = [0x01, 0x02, 0x03];
            byte[] largeData = new byte[1000];
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }
            var obfuscator = new XorDataObfuscator(key);

            // Act
            byte[] obfuscated = obfuscator.Obfuscate(largeData);
            byte[] deobfuscated = obfuscator.Deobfuscate(obfuscated);

            // Assert
            Assert.Equal(largeData, deobfuscated);
            Assert.NotEqual(largeData, obfuscated);
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            yield return new object[] { new byte[] { 0x01 }, new byte[] { 0xAA } };
            yield return new object[] { new byte[] { 0x02, 0x03 }, new byte[] { 0xBB } };
            yield return new object[] { new byte[] { 0x04, 0x05, 0x06 }, new byte[] { 0xCC, 0xDD } };
            yield return new object[] { new byte[100], new byte[] { 0x11, 0x22, 0x33 } };
        }

        [Theory]
        [MemberData(nameof(GetTestCases))]
        public void ObfuscateDeobfuscate_RoundTrip_ReturnsOriginalData(byte[] data, byte[] key)
        {
            // Arrange
            var obfuscator = new XorDataObfuscator(key);

            // Act
            byte[] obfuscated = obfuscator.Obfuscate(data);
            byte[] deobfuscated = obfuscator.Deobfuscate(obfuscated);

            // Assert
            Assert.Equal(data, deobfuscated);
        }

        [Theory]
        [MemberData(nameof(GetTestCases))]
        public void Obfuscate_WithDifferentKeys_ProducesDifferentResults(byte[] data, byte[] key)
        {
            // Arrange
            var obfuscator1 = new XorDataObfuscator(key);
            byte[] differentKey = new byte[key.Length];
            Array.Copy(key, differentKey, key.Length);
            if (differentKey.Length > 0) differentKey[0] ^= 0x01;
            var obfuscator2 = new XorDataObfuscator(differentKey);

            // Act
            byte[] result1 = obfuscator1.Obfuscate(data);
            byte[] result2 = obfuscator2.Obfuscate(data);

            // Assert
            Assert.NotEqual(result1, result2);
        }
    }
}
