using Microsoft.Extensions.Logging;
using NetTunnel.Application.Interfaces;
using System.Net;

namespace NetTunnel.Infrastucture
{
    public class TunnelNode : ITunnelNode, IDisposable
    {
        private readonly ILogger<TunnelNode> _logger;
        private readonly IExternalTransportClient _externalClient;
        private readonly ITunnelTransportClient _tunnelClient;

        private readonly IPEndPoint _externalTargetEndPoint;
        private readonly IPEndPoint _tunnelTargetEndPoint;

        private Task? _externalToTunnelForwardingTask;
        private Task? _tunnelToExternalForwardingTask;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private bool _disposed = false;
        private object _rootLock = new object();

        public TunnelNode(
            ILogger<TunnelNode> logger,
            IExternalTransportClient externalClient,
            ITunnelTransportClient tunnelClient,
            IPEndPoint externalTargetEndPoint,
            IPEndPoint tunnelTargetEndPoint)
        {
            _logger = logger;
            _externalClient = externalClient;
            _tunnelClient = tunnelClient;
            _externalTargetEndPoint = externalTargetEndPoint;
            _tunnelTargetEndPoint = tunnelTargetEndPoint;
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
                _externalToTunnelForwardingTask = ListenExternalAndForwardToTunnelAsync(_cts.Token);
                _tunnelToExternalForwardingTask = ListenTunnelAndForwardToExternalAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Starting tunnel node error: {ex.Message}");
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

            await Task.WhenAll(
                _externalToTunnelForwardingTask ?? Task.CompletedTask,
                _tunnelToExternalForwardingTask ?? Task.CompletedTask);
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

                _externalClient.Dispose();
                _tunnelClient.Dispose();
            }
        }

        private async Task ListenExternalAndForwardToTunnelAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace("Start forwarding external packets");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _externalClient.ReceiveAsync(cancellationToken);
                    var data = result.Data;
                    var remoteEndPoint = result.RemoteEndPoint;

                    _logger.LogDebug("Received external {PacketLength}bytes packet from {RemoteEndPoint}", data.Length, remoteEndPoint);

                    // obfuscate

                    _logger.LogTrace("Forward packet to tunnel {TunnelEndPoint}", _tunnelTargetEndPoint);
                    await _tunnelClient.SendAsync(
                        data: data,
                        endPoint: _tunnelTargetEndPoint,
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"External data forwarding error: {ex.Message}");
                    await Task.Delay(1000);
                }
            }

            _logger.LogTrace("Stop forwarding external packets");
        }

        private async Task ListenTunnelAndForwardToExternalAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace("Start forwarding tunnel packets");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _tunnelClient.ReceiveAsync(cancellationToken);
                    var data = result.Data;
                    var remoteEndPoint = result.RemoteEndPoint;

                    _logger.LogDebug("Received external {PacketLength}bytes packet from {RemoteEndPoint}", data.Length, remoteEndPoint);

                    // deobfuscate

                    _logger.LogTrace("Forward packet to external {ExternalEndPoint}", _externalTargetEndPoint);
                    await _externalClient.SendAsync(
                        data: data,
                        endPoint: _externalTargetEndPoint,
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Tunnel data forwarding error: {ex.Message}");
                    await Task.Delay(1000);
                }
            }

            _logger.LogTrace("Stop forwarding tunnel packets");
        }
    }
}
