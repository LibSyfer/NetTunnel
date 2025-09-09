namespace NetTunnel.Core.Interfaces
{
    internal interface IDataSigner
    {
        byte[] CreateSignature(byte[] data);
        bool VerifySignature(byte[] data, byte[] signature);
    }
}
