using System.Net;

namespace NetTunnel.Application.Interfaces.Sessions
{
    public interface IClientSessionManager
    {
        Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint endPoint, CancellationToken cancellationToken);
        void CleanupInactiveSessions();
    }
}
