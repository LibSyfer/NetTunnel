using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace NetTunnel.Core
{
    public class UdpTunnelClient : IDisposable
    {
        private readonly ILogger<UdpTunnelClient> _logger;
        private readonly UdpClient _listenerClient;
        private readonly UdpClient _forwardClient;
        private readonly HMACSHA256 _hmac;
        private readonly IPEndPoint _serverEndpoint;
        private readonly byte[] _preSharedKey;

        private object _sourceEndpointLock = new object();
        private IPEndPoint? _sourceEndpoint;

        private Task? _processingRequestsTask, _processingRepliesTask;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private bool _isDisposed = false;
        private object _lock = new object();

        private const int _signLength = 32;

        public IPEndPoint? SourceEndpoint
        {
            get { lock(_sourceEndpointLock) { return _sourceEndpoint; } }
            private set { lock(_sourceEndpointLock) { _sourceEndpoint = value; } }
        }

        public UdpTunnelClient(ILogger<UdpTunnelClient> logger, IPEndPoint listenEndpoint, IPEndPoint serverEndpoint, string preSharedKey)
        {
            _logger = logger;
            _listenerClient = new UdpClient(listenEndpoint);
            _forwardClient = new UdpClient(0);
            _preSharedKey = Encoding.UTF8.GetBytes(preSharedKey);
            _hmac = new HMACSHA256(_preSharedKey);
            _serverEndpoint = serverEndpoint;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning || _isDisposed) return Task.CompletedTask;

            lock (_lock)
            {
                if (_isRunning || _isDisposed) return Task.CompletedTask;
                _isRunning = true;

                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

            try
            {
                _processingRequestsTask = ProcessRequestsAsync(_cts.Token);
                _processingRepliesTask = ProcessRepliesAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Starting error: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (!_isRunning ) return;

            lock (_lock)
            {
                if (!_isRunning) return;
                _isRunning = false;

                _cts?.Cancel();
            }

            await Task.WhenAll(_processingRequestsTask!, _processingRepliesTask!);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            lock(_lock)
            {
                if (_isDisposed) return;
                _isDisposed = true;

                _cts?.Dispose();
                _hmac?.Dispose();
                _listenerClient?.Dispose();
                _forwardClient?.Dispose();
            }
        }

        private async Task ProcessRequestsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start processing requests packets on {ListenEndpoint}", _listenerClient.Client.LocalEndPoint);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _listenerClient.ReceiveAsync(cancellationToken);
                    var packet = result.Buffer;
                    var packetLength = result.Buffer.Length;

                    _logger.LogDebug("Receive {DatagramLength}bytes from {TargerEndpoint}", packetLength, result.RemoteEndPoint);
                    var sourceEndpoint = SourceEndpoint;
                    if (sourceEndpoint == null || !sourceEndpoint.Equals(result.RemoteEndPoint))
                    {
                        _logger.LogInformation("Changing source endpoint");
                        SourceEndpoint = result.RemoteEndPoint;
                    }

                    var tunnelPacketLength = packetLength + _signLength;
                    var tunnelPacketBuffer = ArrayPool<byte>.Shared.Rent(tunnelPacketLength);

                    try
                    {
                        var sign = _hmac.ComputeHash(packet, 0, packetLength);

                        Buffer.BlockCopy(packet, 0, tunnelPacketBuffer, 0, packetLength);
                        Buffer.BlockCopy(sign, 0, tunnelPacketBuffer, packetLength, _signLength);

                        await _forwardClient.SendAsync(
                            new ReadOnlyMemory<byte>(tunnelPacketBuffer, 0, tunnelPacketLength),
                            _serverEndpoint,
                            cancellationToken);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(tunnelPacketBuffer);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Processing requests error: {ErrorMessage}", ex.Message);
                    break;
                }
            }

            _logger.LogInformation("Stop processing requests packets on {ListenEndpoint}", _listenerClient.Client.LocalEndPoint);
        }

        private async Task ProcessRepliesAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start processing replies packets from {ListenEndpoint}", _serverEndpoint);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _forwardClient.ReceiveAsync(cancellationToken);
                    var tunnelPacket = result.Buffer;
                    var tunnelPacketLength = result.Buffer.Length;

                    if (tunnelPacketLength < _signLength)
                    {
                        _logger.LogInformation("Receive reply packet with wrong length");
                        continue;
                    }

                    var packetLength = result.Buffer.Length - _signLength;

                    var packetBuffer = ArrayPool<byte>.Shared.Rent(packetLength);
                    var signBuffer = ArrayPool<byte>.Shared.Rent(_signLength);

                    try
                    {
                        Buffer.BlockCopy(result.Buffer, 0, packetBuffer, 0, packetLength);
                        Buffer.BlockCopy(result.Buffer, packetLength, signBuffer, 0, _signLength);

                        var expectedSign = _hmac.ComputeHash(packetBuffer, 0, packetLength);

                        var isValid = CryptographicOperations.FixedTimeEquals(
                            new ReadOnlySpan<byte>(expectedSign, 0, _signLength),
                            new ReadOnlySpan<byte>(signBuffer, 0, _signLength));

                        if (!isValid)
                        {
                            _logger.LogInformation("Receive reply packet with wrong singature");
                            continue;
                        }

                        if (SourceEndpoint == null)
                        {
                            _logger.LogError("Source endpoint is null. Drop packet");
                            continue;
                        }

                        await _listenerClient.SendAsync(
                            new ReadOnlyMemory<byte>(packetBuffer, 0, packetLength),
                            SourceEndpoint,
                            cancellationToken);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(packetBuffer);
                        ArrayPool<byte>.Shared.Return(signBuffer);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Processing replies error: {ErrorMessage}", ex.Message);
                    break;
                }
            }

            _logger.LogInformation("Stop processing replies packets from {ListenEndpoint}", _serverEndpoint);
        }
    }
}
