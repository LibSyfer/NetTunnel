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

        public DateTime LastActivity { get; private set; } = DateTime.UtcNow;

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

            LastActivity = DateTime.UtcNow;
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
                    var packet = result.Buffer;
                    var packetLength = result.Buffer.Length;

                    LastActivity = DateTime.UtcNow;

                    _logger.LogDebug("Receive {DatagramLength}bytes from {TargerEndpoint}", packetLength, result.RemoteEndPoint);

                    var tunnelPacketLength = packetLength + _signLength;
                    var tunnelPacketBuffer = ArrayPool<byte>.Shared.Rent(tunnelPacketLength);

                    try
                    {
                        var sign = _hmac.ComputeHash(packet, 0, packetLength);

                        Buffer.BlockCopy(packet, 0, tunnelPacketBuffer, 0, packetLength);
                        Buffer.BlockCopy(sign, 0, tunnelPacketBuffer, packetLength, _signLength);

                        await _replyClient.SendAsync(
                            new ReadOnlyMemory<byte>(tunnelPacketBuffer, 0, tunnelPacketLength),
                            _replyEndpoint,
                            cancellationToken);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(tunnelPacketBuffer);
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
