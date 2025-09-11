using NetTunnel.Application.Entities;
using System.Net;

namespace NetTunnel.Application.Interfaces
{
    public interface ITransportClient : IDisposable
    {
        IPEndPoint? EndPoint { get; }
        Task<TransportClientResult> ReceiveAsync(CancellationToken cancellationToken);
        Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint endPoint, CancellationToken cancellationToken);
    }

    public interface IExternalTransportClient : ITransportClient;

    public interface ITunnelTransportClient : ITransportClient;
}
