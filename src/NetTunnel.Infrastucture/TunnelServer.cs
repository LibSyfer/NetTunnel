using Microsoft.Extensions.Logging;
using NetTunnel.Application.Entities;
using NetTunnel.Application.Interfaces;
using NetTunnel.Domain.Interfaces;
using System.Collections.Concurrent;
using System.Net;

namespace NetTunnel.Infrastucture
{
    public class TunnelServer : ITunnelNode, IDisposable
    {
        private readonly ILogger<TunnelServer> _logger;
        private readonly IDataObfuscator _obfuscator;
        private readonly IDataSigner _tunnelSigner;
        private readonly IDataSigner _externalSigner;
        private readonly ITunnelPacketBuilder<DefaultTunnelPacket> _packetBuilder;

        private readonly ITunnelTransportClient _tunnelClient;
        private readonly IExternalClientSessionFactory _sessionsFactory;

        private readonly IPEndPoint _targetEndpoint;

        private Task? _processingTunnelDataTask;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private bool _disposed = false;
        private object _rootLock = new();

        private ConcurrentDictionary<IPEndPoint, IExternalClientSession<DefaultTunnelPacket>> _sessions = new();

        public TunnelServer(ILogger<TunnelServer> logger,
            IDataObfuscator obfuscator,
            IDataSigner tunnelSigner,
            IDataSigner externalSigner,
            ITunnelPacketBuilder<DefaultTunnelPacket> packetBuilder,
            ITunnelTransportClient tunnelClient,
            IExternalClientSessionFactory sessionsFactory,
            IPEndPoint targetEndpoint)
        {
            _logger = logger;
            _obfuscator = obfuscator;
            _tunnelSigner = tunnelSigner;
            _externalSigner = externalSigner;
            _packetBuilder = packetBuilder;
            _tunnelClient = tunnelClient;
            _sessionsFactory = sessionsFactory;
            _targetEndpoint = targetEndpoint;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning || _disposed) return Task.CompletedTask;

            lock (_rootLock)
            {
                if (_isRunning || _disposed) return Task.CompletedTask;
                _isRunning = true;

                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

            try
            {
                _processingTunnelDataTask = ProcessTunnelDataAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Start error: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning) return;

            lock (_rootLock)
            {
                if (!_isRunning) return;
                _isRunning = false;

                _cts?.Cancel();
            }

            foreach (var session in _sessions)
            {
                await session.Value.StopAsync(cancellationToken);
            }

            await (_processingTunnelDataTask ?? Task.CompletedTask);
        }

        public void Dispose()
        {
            if (_disposed) return;

            lock (_rootLock)
            {
                if (_disposed) return;
                _disposed = true;

                _cts?.Cancel();
                _cts?.Dispose();

                _tunnelClient.Dispose();

                foreach(var session in _sessions)
                {
                    session.Value.Dispose();
                }
            }
        }

        private async Task ProcessTunnelDataAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start processing tunnel data on {ListenEndpoint}", _tunnelClient.EndPoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Processing tunnel data error: {ex.Message}");
                    await Task.Delay(1000);
                }
            }

            _logger.LogInformation("Stop processing tunnel data");
        }
    }
}
