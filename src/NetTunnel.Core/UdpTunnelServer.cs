using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace NetTunnel.Core
{
    public class UdpTunnelServer : IDisposable
    {
        private readonly ILogger<UdpTunnelServer> _logger;
        private readonly ILoggerFactory _sessionLoggerFactory;
        private readonly UdpClient _listenerClient;
        private readonly IPEndPoint _targetEndpoint;
        private readonly HMACSHA256 _hmac;
        private readonly byte[] _preSharedKey;
        private ConcurrentDictionary<IPEndPoint, UdpClientSession> _sessions = new();

        private Task? _processingRequestsTask;

        private Task? _cleanupSessionsTask;
        private readonly TimeSpan _sessionTimeout;
        private readonly TimeSpan _cleanupDelay;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private bool _isDisposed = false;
        private object _lock = new object();

        private const int _signLength = 32;

        public UdpTunnelServer(ILogger<UdpTunnelServer> logger,
            ILoggerFactory sessionLoggerFactory,
            IPEndPoint listenerEndpoint,
            int targetLocalPort, string preSharedKey,
            TimeSpan sessionTimeout,
            TimeSpan cleanupDelay)
        {
            _logger = logger;
            _sessionLoggerFactory = sessionLoggerFactory;
            _listenerClient = new UdpClient(listenerEndpoint);
            _preSharedKey = Encoding.UTF8.GetBytes(preSharedKey);
            _hmac = new HMACSHA256(_preSharedKey);
            _targetEndpoint = new IPEndPoint(IPAddress.Loopback, targetLocalPort);
            _sessionTimeout = sessionTimeout;
            _cleanupDelay = cleanupDelay;
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
                _cleanupSessionsTask = CleanupSessionsAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Starting error: {ErrorMessage}", ex.Message);
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (!_isRunning) return;

            lock (_lock)
            {
                if (!_isRunning) return;
                _isRunning = false;

                _cts?.Cancel();
            }

            await Task.WhenAll(
                _processingRequestsTask ?? Task.CompletedTask,
                _cleanupSessionsTask ?? Task.CompletedTask);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            lock (_lock)
            {
                if (_isDisposed) return;
                _isDisposed = true;

                _cts?.Cancel();
                _cts?.Dispose();
                _hmac?.Dispose();
                _listenerClient?.Dispose();

                foreach (var session in _sessions.Values)
                {
                    session.Dispose();
                }
                _sessions.Clear();
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
                    var tunnelPacket = result.Buffer;
                    var tunnelPacketLength = result.Buffer.Length;
                    var tunnelClientEndpoint = result.RemoteEndPoint;

                    _logger.LogDebug("Receive tunnel packet {DatagramLength}bytes from {TargerEndpoint}", tunnelPacketLength, tunnelClientEndpoint);

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
                            _logger.LogWarning("Receive reply packet with wrong singature");
                            continue;
                        }

                        var sessionLogger = _sessionLoggerFactory
                            .CreateLogger($"{typeof(UdpTunnelServer).FullName}-Session-{tunnelClientEndpoint}");
                        var session = _sessions.GetOrAdd(tunnelClientEndpoint, e =>
                            new UdpClientSession(sessionLogger, _hmac, _listenerClient, tunnelClientEndpoint));

                        await session.SendAsync(
                            new ReadOnlyMemory<byte>(packetBuffer, 0, packetLength),
                            _targetEndpoint,
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
                    _logger.LogError(ex, "Process request error: {ErrorMessage}", ex.Message);
                    break;
                }
            }

            _logger.LogInformation("Stop processing requests packets");
        }

        private async Task CleanupSessionsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start session cleanup");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_cleanupDelay, cancellationToken);

                    var cutoff = DateTime.UtcNow - _sessionTimeout;
                    var removedCount = 0;

                    foreach (var key in _sessions.Keys.ToArray())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (_sessions.TryGetValue(key, out var session) &&
                            session.LastActivity < cutoff &&
                            _sessions.TryRemove(key, out var removedSession))
                        {
                            removedSession.Dispose();
                            ++removedCount;
                        }
                    }

                    if (removedCount > 0)
                    {
                        _logger.LogInformation("Removed {InactiveSessionsCount} inactive sessions", removedCount);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Session cleanup error");
                }
            }

            _logger.LogInformation("Stop session cleanup");
        }
    }
}
