namespace NetTunnel.Domain.Interfaces
{
    public interface IDataSigner
    {
        byte[] CreateSignature(byte[] data);
        bool VerifySignature(byte[] data, byte[] signature);
    }
}
