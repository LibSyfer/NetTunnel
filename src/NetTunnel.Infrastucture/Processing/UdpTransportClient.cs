using NetTunnel.Application.Entities;
using NetTunnel.Application.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace NetTunnel.Infrastucture.Processing
{
    public class UdpTransportClient : IExternalTransportClient, ITunnelTransportClient
    {
        private readonly UdpClient _client;

        public IPEndPoint? EndPoint => _client.Client.LocalEndPoint as IPEndPoint;

        public UdpTransportClient(IPEndPoint listenEndpoint)
        {
            _client = new UdpClient(listenEndpoint);
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            return await _client.SendAsync(
                datagram: data,
                endPoint: endPoint,
                cancellationToken: cancellationToken);
        }

        public async Task<TransportClientResult> ReceiveAsync(CancellationToken cancellationToken)
        {
            var receiveResult = await _client.ReceiveAsync(cancellationToken);

            return new TransportClientResult
            {
                Data = receiveResult.Buffer,
                RemoteEndPoint = receiveResult.RemoteEndPoint
            };
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
