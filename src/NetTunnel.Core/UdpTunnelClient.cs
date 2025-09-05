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

        public UdpTunnelClient(ILogger<UdpTunnelClient> logger, int listenerLocalPort, IPEndPoint serverEndpoint, string preSharedKey)
        {
            _logger = logger;
            _listenerClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, listenerLocalPort));
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

                    if (SourceEndpoint != result.RemoteEndPoint)
                    {
                        _logger.LogInformation("Changing source endpoint");
                        SourceEndpoint = result.RemoteEndPoint;
                    }

                    var buffer = ArrayPool<byte>.Shared.Rent(packetLength + _signLength);

                    try
                    {
                        var sign = _hmac.ComputeHash(result.Buffer);

                        Buffer.BlockCopy(result.Buffer, 0, buffer, 0, result.Buffer.Length);
                        Buffer.BlockCopy(sign, 0, buffer, result.Buffer.Length, sign.Length);

                        await _forwardClient.SendAsync(new ReadOnlyMemory<byte>(buffer), _serverEndpoint, cancellationToken);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
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
                    if (result.Buffer.Length < _signLength)
                    {
                        _logger.LogInformation("Receive reply packet with wrong singature");
                        continue;
                    }

                    var packetLength = result.Buffer.Length - _signLength;

                    var buffer = ArrayPool<byte>.Shared.Rent(packetLength);
                    var signBuffer = ArrayPool<byte>.Shared.Rent(_signLength);

                    try
                    {
                        Buffer.BlockCopy(result.Buffer, 0, buffer, 0, packetLength);
                        Buffer.BlockCopy(result.Buffer, packetLength, signBuffer, 0, _signLength);

                        var expectedSign = _hmac.ComputeHash(buffer, 0, packetLength);

                        var isValid = CryptographicOperations.FixedTimeEquals(expectedSign, signBuffer);

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

                        await _listenerClient.SendAsync(new ReadOnlyMemory<byte>(buffer, 0, packetLength), SourceEndpoint, cancellationToken);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
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
