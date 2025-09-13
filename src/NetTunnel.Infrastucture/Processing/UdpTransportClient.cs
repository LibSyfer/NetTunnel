using Microsoft.Extensions.Logging;
using NetTunnel.Application.Entities;
using NetTunnel.Application.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace NetTunnel.Infrastucture.Processing
{
    public class UdpTransportClient : IExternalTransportClient, ITunnelTransportClient
    {
        private readonly ILogger<UdpTransportClient> _logger;
        private readonly UdpClient _client;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public UdpTransportClient(ILogger<UdpTransportClient> logger,
            IPEndPoint listenEndpoint)
        {
            _logger = logger;
            _client = new UdpClient(listenEndpoint);
        }

        public IPEndPoint? EndPoint => _client.Client.LocalEndPoint as IPEndPoint;

        public async Task<TransportClientResult> ReceiveAsync(CancellationToken cancellationToken)
        {
                var udpReceiveResult = await _client.ReceiveAsync(cancellationToken);
                _logger.LogDebug("Receive {BytesLength}bytes from {RemoteEndpoint}", udpReceiveResult.Buffer, udpReceiveResult.RemoteEndPoint);

                return new TransportClientResult
                {
                    Data = udpReceiveResult.Buffer,
                    RemoteEndPoint = udpReceiveResult.RemoteEndPoint,
                };
            }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint endPoint, CancellationToken cancellationToken)
        {
                _logger.LogDebug("Send {BytesLength}bytes to {TargetEndpoint}", data.Length, endPoint);
                return await _client.SendAsync(data, endPoint, cancellationToken);
            }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
