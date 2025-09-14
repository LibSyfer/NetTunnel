using Microsoft.Extensions.Logging;
using NetTunnel.Application.Entities;
using NetTunnel.Application.Interfaces;
using NetTunnel.Application.Interfaces.Sessions;
using NetTunnel.Domain.Interfaces;
using System.Net;

namespace NetTunnel.Infrastucture
{
    public class TunnelServer : ITunnelNode, IDisposable
    {
        private readonly ILogger<TunnelServer> _logger;
        private readonly IDataObfuscator _obfuscator;
        private readonly IDataSigner _tunnelSigner;
        private readonly ITunnelPacketBuilder<DefaultTunnelPacket> _packetBuilder;

        private readonly ITunnelTransportClient _tunnelClient;
        private readonly IClientSessionManager _sessionManager;

        private readonly IPEndPoint _targetEndpoint;

        private Task? _processingTunnelDataTask;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private bool _disposed = false;
        private object _rootLock = new();

        public TunnelServer(ILogger<TunnelServer> logger,
            IDataObfuscator obfuscator,
            IDataSigner tunnelSigner,
            ITunnelPacketBuilder<DefaultTunnelPacket> packetBuilder,
            ITunnelTransportClient tunnelClient,
            IClientSessionManager sessionManager,
            IPEndPoint targetEndpoint)
        {
            _logger = logger;
            _obfuscator = obfuscator;
            _tunnelSigner = tunnelSigner;
            _packetBuilder = packetBuilder;
            _tunnelClient = tunnelClient;
            _sessionManager = sessionManager;
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
            }
        }

        private async Task ProcessTunnelDataAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start processing tunnel data on {ListenEndpoint}", _tunnelClient.EndPoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _tunnelClient.ReceiveAsync(cancellationToken);
                    var data = result.Data;

                    var tunnelPacket = _packetBuilder.ParsePacket(data);

                    if (!_tunnelSigner.VerifySignature(tunnelPacket.Data, tunnelPacket.Sign))
                    {
                        _logger.LogWarning("Tunnel data has wrong signature");
                        continue;
                    }

                    var deobfuscatePacket = _obfuscator.Deobfuscate(tunnelPacket.Data);

                    await _sessionManager.SendAsync(
                        data: deobfuscatePacket,
                        remoteEndPoint: result.RemoteEndPoint,
                        targetEndPoint: _targetEndpoint,
                        cancellationToken: cancellationToken);
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
