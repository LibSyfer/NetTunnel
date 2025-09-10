using Microsoft.Extensions.Logging;
using NetTunnel.Domain.Entities;
using NetTunnel.Domain.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace NetTunnel.Infrastucture
{
    public class UdpTunnelClient : ITunnelNode, IDisposable
    {
        private readonly ILogger<UdpTunnelClient> _logger;
        private readonly IDataObfuscator _obfuscator;
        private readonly IDataSigner _signer;
        private readonly ITunnelPacketBuilder<DefaultTunnelPacket> _packetBuilder;

        private readonly UdpClient _listenClient;
        private readonly UdpClient _forwardClient;
        private readonly IPEndPoint _serverEndpoint;

        private Task? _processingListeningPacketsTask;
        private Task? _processingReplyingPacketsTask;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private bool _disposed = false;
        private object _rootLock = new();

        public UdpTunnelClient(ILogger<UdpTunnelClient> logger,
            IDataObfuscator obfuscator,
            IDataSigner signer,
            ITunnelPacketBuilder<DefaultTunnelPacket> packetBuilder,
            IPEndPoint listenEndpoint,
            IPEndPoint serverEndpoint)
        {
            _logger = logger;
            _obfuscator = obfuscator;
            _signer = signer;
            _packetBuilder = packetBuilder;

            _listenClient = new UdpClient(listenEndpoint);
            _forwardClient = new UdpClient(0);
            _serverEndpoint = serverEndpoint;
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
                _processingListeningPacketsTask = ProcessListeningPacketsAsync(_cts.Token);
                _processingReplyingPacketsTask = ProcessReplyingPacketsAsync(_cts.Token);
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
                _processingListeningPacketsTask ?? Task.CompletedTask,
                _processingReplyingPacketsTask ?? Task.CompletedTask);
        }

        public void Dispose()
        {
            if (_disposed) return;

            lock(_rootLock)
            {
                if (_disposed) return;
                _disposed = true;

                _cts?.Cancel();
                _listenClient.Close();
                _forwardClient.Close();
                _listenClient.Dispose();
                _forwardClient.Dispose();
            }
        }

        private async Task ProcessListeningPacketsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start processing listening packets on {ListenEndpoint}", _listenClient.Client.LocalEndPoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var receiveResult = await _listenClient.ReceiveAsync(cancellationToken);
                    var packet = receiveResult.Buffer;
                    var packetLength = receiveResult.Buffer.Length;

                    var obfuscatePacket = _obfuscator.Obfuscate(packet);
                    var sign = _signer.CreateSignature(obfuscatePacket);

                    var tunnelPacket = new DefaultTunnelPacket
                    {
                        Data = obfuscatePacket,
                        Sign = sign,
                    };

                    var tunnelRawData = _packetBuilder.BuildPacket(tunnelPacket);

                    await _forwardClient.SendAsync(
                        datagram: tunnelRawData.AsMemory(),
                        endPoint: _serverEndpoint,
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Processing listening packets error: {ex.Message}");
                    await Task.Delay(1000);
                }
            }

            _logger.LogInformation("Stop processing listening packets");
        }

        private async Task ProcessReplyingPacketsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start processing replying packets on {ListenEndpoint}", _listenClient.Client.LocalEndPoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var receiveResult = await _listenClient.ReceiveAsync(cancellationToken);
                    var tunnelRawData = receiveResult.Buffer;

                    var tunnelPacket = _packetBuilder.ParsePacket(tunnelRawData);

                    if (!_signer.VerifySignature(tunnelPacket.Data, tunnelPacket.Sign))
                    {
                        _logger.LogWarning("Replying packet has wrong signature");
                        continue;
                    }

                    var deobfuscatePacket = _obfuscator.Deobfuscate(tunnelPacket.Data);

                    await _listenClient.SendAsync(
                        datagram: deobfuscatePacket.AsMemory(), 
                        endPoint: _serverEndpoint,
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Processing replying packets error: {ex.Message}");
                    await Task.Delay(1000);
                }
            }

            _logger.LogInformation("Stop processing replying packets");
        }
    }
}
