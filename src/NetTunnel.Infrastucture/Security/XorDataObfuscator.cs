using NetTunnel.Domain.Interfaces;

namespace NetTunnel.Infrastucture.Security
{
    public class XorDataObfuscator : IDataObfuscator
    {
        private readonly byte[] _key;

        public XorDataObfuscator(byte[] key)
        {
            _key = key ?? throw new ArgumentNullException(nameof(key));
        }

        public byte[] Deobfuscate(byte[] data) => XorData(data);

        public byte[] Obfuscate(byte[] data) => XorData(data); 

        private byte[] XorData(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty");

            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; ++i)
            {
                result[i] = (byte)(data[i] ^ _key[i % _key.Length]);
            }

            return result;
        }
    }
}
