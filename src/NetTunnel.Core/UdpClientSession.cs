using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace NetTunnel.Core
{
    internal class UdpClientSession : IDisposable
    {
        private readonly ILogger _logger;
        private readonly HMACSHA256 _hmac;
        private readonly UdpClient _replyClient;
        private readonly UdpClient _targetClient;
        private readonly IPEndPoint _replyEndpoint;
        private readonly CancellationTokenSource _cts;

        private const int _signLength = 32;

        public UdpClientSession(ILogger logger, HMACSHA256 hmac, UdpClient replyClient, IPEndPoint replyEndpoint)
        {
            _logger = logger;
            _hmac = hmac;
            _replyClient = replyClient;
            _targetClient = new UdpClient(0);
            _replyEndpoint = replyEndpoint;
            _cts = new CancellationTokenSource();

            _ = ProcessSessionReplies(_cts.Token);
        }

        public async Task SendAsync(ReadOnlyMemory<byte> datagram, IPEndPoint targetEndPoint, CancellationToken  cancellationToken)
        {
            _logger.LogDebug("Send {DatagramLength}bytes to {TargerEndpoint}", datagram.Length, targetEndPoint);
            await _targetClient.SendAsync(datagram, targetEndPoint, cancellationToken);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _targetClient.Dispose();
        }

        private async Task ProcessSessionReplies(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _targetClient.ReceiveAsync(cancellationToken);
                    _logger.LogDebug("Receive {DatagramLength}bytes from {TargerEndpoint}", result.Buffer.Length, result.RemoteEndPoint);

                    var packet = result.Buffer;
                    var packetLength = result.Buffer.Length;

                    var buffer = ArrayPool<byte>.Shared.Rent(packetLength + _signLength);
                    try
                    {
                        var sign = _hmac.ComputeHash(packet);

                        Buffer.BlockCopy(packet, 0, buffer, 0, packetLength);
                        Buffer.BlockCopy(sign, 0, buffer, packetLength, sign.Length);

                        _logger.LogDebug("Send {DatagramLength}bytes to {TargerEndpoint}", packetLength, _replyEndpoint);
                        await _replyClient.SendAsync(new ReadOnlyMemory<byte>(buffer), _replyEndpoint, cancellationToken);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reply receiving error: {ErrorMessage}", ex.Message);
                }
            }
        }
    }
}
