using System.Net;

namespace NetTunnel.Application.Interfaces.Sessions
{
    public interface IClientSession : IDisposable
    {
        Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint endPoint, CancellationToken cancellationToken);
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
