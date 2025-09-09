namespace NetTunnel.Core.Interfaces
{
    internal interface IDataObfuscator
    {
        byte[] Obfuscate(byte[] data);
        byte[] Deobfuscate(byte[] data);
    }
}
