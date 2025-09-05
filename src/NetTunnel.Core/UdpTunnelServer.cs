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

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private object _lock = new object();

        private const int _signLength = 32;

        public UdpTunnelServer(ILogger<UdpTunnelServer> logger, ILoggerFactory sessionLoggerFactory, IPEndPoint listenerEndpoint, int targetLocalPort, string preSharedKey)
        {
            _logger = logger;
            _sessionLoggerFactory = sessionLoggerFactory;
            _listenerClient = new UdpClient(listenerEndpoint);
            _preSharedKey = Encoding.UTF8.GetBytes(preSharedKey);
            _hmac = new HMACSHA256(_preSharedKey);
            _targetEndpoint = new IPEndPoint(IPAddress.Loopback, targetLocalPort);
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning) return Task.CompletedTask;

            lock (_lock)
            {
                if (_isRunning) return Task.CompletedTask;
                _isRunning = true;

                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

            try
            {
                _processingRequestsTask = ProcessRequestsAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Starting error: {ErrorMessage}", ex.Message);
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (! _isRunning) return;

            lock (_lock)
            {
                if (!_isRunning) return;
                _isRunning = false;

                _cts?.Cancel();
            }

            await _processingRequestsTask!;
        }

        public void Dispose()
        {
            _cts?.Dispose();
            _hmac?.Dispose();
            _listenerClient?.Dispose();

            foreach (var session in _sessions.Values)
            {
                session.Dispose();
            }
            _sessions.Clear();
        }

        private async Task ProcessRequestsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start processing requests packets");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _listenerClient.ReceiveAsync(cancellationToken);
                    var clientEndpoint = result.RemoteEndPoint;
                    if (result.Buffer.Length < _signLength)
                    {
                        _logger.LogInformation("Receive reply packet with wrong singature");
                        continue;
                    }

                    var packetLength = result.Buffer.Length - _signLength;

                    var packetBuffer = ArrayPool<byte>.Shared.Rent(packetLength);
                    var signBuffer = ArrayPool<byte>.Shared.Rent(_signLength);

                    try
                    {
                        Buffer.BlockCopy(result.Buffer, 0, packetBuffer, 0, packetLength);
                        Buffer.BlockCopy(result.Buffer, packetLength, signBuffer, 0, _signLength);

                        var expectedSign = _hmac.ComputeHash(packetBuffer);

                        var isValid = CryptographicOperations.FixedTimeEquals(expectedSign, signBuffer);

                        if (!isValid)
                        {
                            _logger.LogInformation("Receive reply packet with wrong singature");
                            continue;
                        }

                        var sessionLogger = _sessionLoggerFactory
                        .CreateLogger($"{typeof(UdpTunnelServer).FullName}-Session-{clientEndpoint}");
                        var session = _sessions.GetOrAdd(clientEndpoint, e =>
                            new UdpClientSession(sessionLogger, _hmac, _listenerClient, clientEndpoint));

                        _logger.LogDebug("Send {DatagramLength}bytes to {TargerEndpoint}", packetLength, _targetEndpoint);
                        await session.SendAsync(packetBuffer, _targetEndpoint, cancellationToken);
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
                }
            }

            _logger.LogInformation("Stop processing requests packets");
        }
    }
}
