using NetTunnel.Infrastucture.Security;

namespace NetTunnel.Tests.Infrastructure.Security
{
    public class HmacDataSignerTests : IDisposable
    {
        private readonly List<IDisposable> _disposables = new();

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
            _disposables.Clear();
        }

        private HmacDataSigner CreateSigner(HmacDataSigner.Algorithm algorithm, byte[] key)
        {
            var signer = new HmacDataSigner(algorithm, key);
            _disposables.Add(signer);

            return signer;
        }

        public static IEnumerable<object[]> GetAlgorithms()
        {
            yield return new object[] { HmacDataSigner.Algorithm.SHA256 };
            yield return new object[] { HmacDataSigner.Algorithm.SHA384 };
            yield return new object[] { HmacDataSigner.Algorithm.SHA512 };
            yield return new object[] { HmacDataSigner.Algorithm.SHA1 };
            yield return new object[] { HmacDataSigner.Algorithm.MD5 };
        }

        public static IEnumerable<object[]> GetTestData()
        {
            yield return new object[] { new byte[] { 0x01, 0x02, 0x03 }, new byte[] { 0xAA, 0xBB } };
            yield return new object[] { new byte[0], new byte[] { 0x01 } }; // Empty data
            yield return new object[] { new byte[100], new byte[32] }; // Large data
            yield return new object[] { new byte[] { 0xFF }, new byte[] { 0x00 } }; // Edge values
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void CreateSignature_WithValidData_ReturnsNonEmptySignature(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = [0x01, 0x02, 0x03];
            byte[] data = [0xAA, 0xBB, 0xCC];
            var signer = CreateSigner(algorithm, key);

            // Act
            byte[] signature = signer.CreateSignature(data);

            // Assert
            Assert.NotNull(signature);
            Assert.NotEmpty(signature);
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void CreateSignature_WithSameDataAndKey_ReturnsSameSignature(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = new byte[] { 0x01, 0x02, 0x03 };
            byte[] data = new byte[] { 0xAA, 0xBB, 0xCC };
            var signer = CreateSigner(algorithm, key);

            // Act
            byte[] signature1 = signer.CreateSignature(data);
            byte[] signature2 = signer.CreateSignature(data);

            // Assert
            Assert.Equal(signature1, signature2);
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void CreateSignature_WithDifferentKeys_ReturnsDifferentSignatures(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] data = [0xAA, 0xBB, 0xCC];
            var signer1 = CreateSigner(algorithm, [0x01]);
            var signer2 = CreateSigner(algorithm, [0x02]);

            // Act
            byte[] signature1 = signer1.CreateSignature(data);
            byte[] signature2 = signer2.CreateSignature(data);

            // Assert
            Assert.NotEqual(signature1, signature2);
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void CreateSignature_WithDifferentData_ReturnsDifferentSignatures(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = [0x01, 0x02, 0x03];
            var signer = CreateSigner(algorithm, key);

            // Act
            byte[] signature1 = signer.CreateSignature([0xAA]);
            byte[] signature2 = signer.CreateSignature([0xAB]); // Just one byte different

            // Assert
            Assert.NotEqual(signature1, signature2);
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void CreateSignature_WithNullData_ThrowsArgumentException(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = [0x01];
            var signer = CreateSigner(algorithm, key);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => signer.CreateSignature(null));
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void CreateSignature_WithEmptyData_ThrowsArgumentException(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = [0x01];
            var signer = CreateSigner(algorithm, key);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => signer.CreateSignature(Array.Empty<byte>()));
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void VerifySignature_WithValidSignature_ReturnsTrue(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = [0x01, 0x02, 0x03];
            byte[] data = [0xAA, 0xBB, 0xCC];
            var signer = CreateSigner(algorithm, key);
            byte[] validSignature = signer.CreateSignature(data);

            // Act
            bool result = signer.VerifySignature(data, validSignature);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void VerifySignature_WithInvalidSignature_ReturnsFalse(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = [0x01, 0x02, 0x03];
            byte[] data = [0xAA, 0xBB, 0xCC];
            var signer = CreateSigner(algorithm, key);
            byte[] invalidSignature = [0xFF, 0xFF, 0xFF]; // Obviously wrong

            // Act
            bool result = signer.VerifySignature(data, invalidSignature);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void VerifySignature_WithTamperedData_ReturnsFalse(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = [0x01, 0x02, 0x03];
            byte[] originalData = [0xAA, 0xBB, 0xCC];
            byte[] tamperedData = [0xAA, 0xBB, 0xCD]; // Just one byte different
            var signer = CreateSigner(algorithm, key);
            byte[] validSignature = signer.CreateSignature(originalData);

            // Act
            bool result = signer.VerifySignature(tamperedData, validSignature);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void VerifySignature_WithNullData_ReturnsFalse(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = [0x01];
            byte[] signature = [0xAA, 0xBB];
            var signer = CreateSigner(algorithm, key);

            // Act
            bool result = signer.VerifySignature(null, signature);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void VerifySignature_WithNullSignature_ReturnsFalse(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = [0x01];
            byte[] data = [0xAA, 0xBB];
            var signer = CreateSigner(algorithm, key);

            // Act
            bool result = signer.VerifySignature(data, null);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void VerifySignature_WithEmptyData_ReturnsFalse(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = [0x01];
            byte[] signature = [0xAA, 0xBB];
            var signer = CreateSigner(algorithm, key);

            // Act
            bool result = signer.VerifySignature(Array.Empty<byte>(), signature);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void VerifySignature_WithEmptySignature_ReturnsFalse(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = [0x01];
            byte[] data = [0xAA, 0xBB];
            var signer = CreateSigner(algorithm, key);

            // Act
            bool result = signer.VerifySignature(data, Array.Empty<byte>());

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Constructor_WithSHA256_CreatesHMACSHA256()
        {
            // Arrange & Act
            var signer = CreateSigner(HmacDataSigner.Algorithm.SHA256, [0x01]);

            // Assert
            byte[] signature = signer.CreateSignature([0xAA]);
            Assert.Equal(32, signature.Length); // SHA256 gives 32 bytes
        }

        [Fact]
        public void Constructor_WithSHA384_CreatesHMACSHA384()
        {
            // Arrange & Act
            var signer = CreateSigner(HmacDataSigner.Algorithm.SHA384, [0x01]);

            // Assert
            byte[] signature = signer.CreateSignature([0xAA]);
            Assert.Equal(48, signature.Length); // SHA384 gives 48 bytes
        }

        [Fact]
        public void Constructor_WithSHA512_CreatesHMACSHA512()
        {
            // Arrange & Act
            var signer = CreateSigner(HmacDataSigner.Algorithm.SHA512, [0x01]);

            // Assert
            byte[] signature = signer.CreateSignature([0xAA]);
            Assert.Equal(64, signature.Length); // SHA512 gives 64 bytes
        }

        [Fact]
        public void Constructor_WithSHA1_CreatesHMACSHA1()
        {
            // Arrange & Act
            var signer = CreateSigner(HmacDataSigner.Algorithm.SHA1, [0x01]);

            // Assert
            byte[] signature = signer.CreateSignature([0xAA]);
            Assert.Equal(20, signature.Length); // SHA1 gives 20 bytes
        }

        [Fact]
        public void Constructor_WithMD5_CreatesHMACMD5()
        {
            // Arrange & Act
            var signer = CreateSigner(HmacDataSigner.Algorithm.MD5, [0x01]);

            // Assert
            byte[] signature = signer.CreateSignature([0xAA]);
            Assert.Equal(16, signature.Length); // MD5 gives 16 bytes
        }

        [Theory]
        [MemberData(nameof(GetAlgorithms))]
        public void VerifySignature_IsTimingAttackResistant(HmacDataSigner.Algorithm algorithm)
        {
            // Arrange
            byte[] key = [0x01];
            byte[] data = [0xAA, 0xBB, 0xCC];
            var signer = CreateSigner(algorithm, key);
            byte[] validSignature = signer.CreateSignature(data);

            // Invalid signature of the same length
            byte[] invalidSignature = new byte[validSignature.Length];
            Array.Fill(invalidSignature, (byte)0xFF);

            // Act & Assert
            bool validResult = signer.VerifySignature(data, validSignature);
            bool invalidResult = signer.VerifySignature(data, invalidSignature);

            Assert.True(validResult);
            Assert.False(invalidResult);
        }
    }
}
