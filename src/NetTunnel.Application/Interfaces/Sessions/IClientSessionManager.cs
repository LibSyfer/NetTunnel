using System.Net;

namespace NetTunnel.Application.Interfaces.Sessions
{
    public interface IClientSessionManager : IDisposable
    {
        Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint remoteEndPoint, IPEndPoint targetEndPoint, CancellationToken cancellationToken);
        void CleanupInactiveSessions();
    }
}
