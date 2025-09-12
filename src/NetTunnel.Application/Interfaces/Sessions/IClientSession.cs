using System.Net;

namespace NetTunnel.Application.Interfaces.Sessions
{
    public interface IClientSession : IDisposable
    {
        DateTime LastActivity { get; }
        Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint endPoint, CancellationToken cancellationToken);
    }
}
