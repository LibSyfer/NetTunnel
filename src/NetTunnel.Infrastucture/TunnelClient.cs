using Microsoft.Extensions.Logging;
using NetTunnel.Application.Entities;
using NetTunnel.Application.Interfaces;
using NetTunnel.Domain.Interfaces;
using System.Net;

namespace NetTunnel.Infrastucture
{
    public class TunnelClient : ITunnelNode, IDisposable
    {
        private readonly ILogger<TunnelClient> _logger;
        private readonly IDataObfuscator _obfuscator;
        private readonly IDataSigner _tunnelSigner;
        private readonly IDataSigner _externalSigner;
        private readonly ITunnelPacketBuilder<DefaultTunnelPacket> _packetBuilder;

        private readonly ITunnelTransportClient _tunnelClient;
        private readonly IExternalTransportClient _externalClient;

        private readonly IPEndPoint _serverEndpoint;
        private IPEndPoint? _externalEndpoint;
        private object _externalEndpointLock = new();

        private Task? _processingExternalDataTask;
        private Task? _processingTunnelDataTask;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private bool _disposed = false;
        private object _rootLock = new();

        public TunnelClient(ILogger<TunnelClient> logger,
            IDataObfuscator obfuscator,
            IDataSigner tunnelSigner,
            IDataSigner externalSigner,
            ITunnelPacketBuilder<DefaultTunnelPacket> packetBuilder,
            ITunnelTransportClient tunnelClient,
            IExternalTransportClient externalClient,
            IPEndPoint serverEndpoint)
        {
            _logger = logger;
            _obfuscator = obfuscator;
            _tunnelSigner = tunnelSigner;
            _externalSigner = externalSigner;
            _packetBuilder = packetBuilder;

            _tunnelClient = tunnelClient;
            _externalClient = externalClient;

            _serverEndpoint = serverEndpoint;
        }

        private IPEndPoint? ExternalEndpoint
        {
            get
            {
                lock (_externalEndpointLock)
                {
                    return _externalEndpoint;
                }
            }
            set
            {
                lock (_externalEndpointLock)
                {
                    _externalEndpoint = value;
                }
            }
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
                _processingExternalDataTask = ProcessExternalDataAsync(_cts.Token);
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

            await Task.WhenAll(
                _processingExternalDataTask ?? Task.CompletedTask,
                _processingTunnelDataTask ?? Task.CompletedTask);
        }

        public void Dispose()
        {
            if (_disposed) return;

            lock(_rootLock)
            {
                if (_disposed) return;
                _disposed = true;

                _cts?.Cancel();
                _cts?.Dispose();

                _externalClient.Dispose();
                _tunnelClient.Dispose();
            }
        }

        private async Task ProcessExternalDataAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start processing external data on {ListenEndpoint}", _externalClient.EndPoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _externalClient.ReceiveAsync(cancellationToken);
                    var data = result.Data;
                    var remoteEndpoint = result.RemoteEndPoint;

                    if (remoteEndpoint != null && !remoteEndpoint.Equals(ExternalEndpoint))
                    {
                        ExternalEndpoint = remoteEndpoint;
                    }

                    var obfuscatePacket = _obfuscator.Obfuscate(data);
                    var sign = _tunnelSigner.CreateSignature(obfuscatePacket);

                    var tunnelPacket = new DefaultTunnelPacket
                    {
                        Data = obfuscatePacket,
                        Sign = sign,
                    };

                    var tunnelRawData = _packetBuilder.BuildPacket(tunnelPacket);

                    await _tunnelClient.SendAsync(
                        data: tunnelRawData,
                        endPoint: _serverEndpoint,
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Processing external data error: {ex.Message}");
                    await Task.Delay(1000);
                }
            }

            _logger.LogInformation("Stop processing external data");
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

                    if (!_externalSigner.VerifySignature(tunnelPacket.Data, tunnelPacket.Sign))
                    {
                        _logger.LogWarning("Tunnel data has wrong signature");
                        continue;
                    }

                    var deobfuscatePacket = _obfuscator.Deobfuscate(tunnelPacket.Data);

                    await _externalClient.SendAsync(
                        data: deobfuscatePacket, 
                        endPoint: ExternalEndpoint ?? throw new ArgumentNullException("Null external endpoint"),
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
