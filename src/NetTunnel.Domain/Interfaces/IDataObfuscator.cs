namespace NetTunnel.Domain.Interfaces
{
    public interface IDataObfuscator
    {
        byte[] Obfuscate(byte[] data);
        byte[] Deobfuscate(byte[] data);
    }
}
