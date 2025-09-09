using Microsoft.Extensions.Logging;
using NetTunnel.Core.Interfaces;
using System.Security.Cryptography;

namespace NetTunnel.Core.Security
{
    internal class HmacDataSigner : IDataSigner, IDisposable
    {
        private readonly HMAC _hmac;

        public enum Algorithm
        {
            SHA256,
            SHA384,
            SHA512,
            SHA1,
            MD5
        }

        public HmacDataSigner(Algorithm algorithm, byte[] key)
        {
            _hmac = algorithm switch
            {
                Algorithm.SHA256 => new HMACSHA256(key),
                Algorithm.SHA384 => new HMACSHA384(key),
                Algorithm.SHA512 => new HMACSHA512(key),
                Algorithm.SHA1 => new HMACSHA1(key),
                _ => new HMACMD5(key)
            };
        }

        public byte[] CreateSignature(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty");

            return _hmac.ComputeHash(data);
        }

        public bool VerifySignature(byte[] data, byte[] signature)
        {
            if (data == null || data.Length == 0)
                return false;

            if (signature == null || signature.Length == 0)
                return false;

            byte[] computedSignature = CreateSignature(data);
            return CryptographicOperations.FixedTimeEquals(computedSignature, signature);
        }

        public void Dispose()
        {
            _hmac.Dispose();
        }
    }
}
