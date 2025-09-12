using Microsoft.Extensions.Logging;
using NetTunnel.Application.Entities;
using NetTunnel.Application.Interfaces;
using NetTunnel.Application.Interfaces.Sessions;
using NetTunnel.Domain.Interfaces;
using System.Net;

namespace NetTunnel.Infrastucture.Sessions
{
    public class DefaultClientSession : IClientSession
    {
        private readonly ILogger _logger;
        private readonly ITransportClient _sessionClient;
        private readonly ITransportClient _replyClient;
        private readonly IDataObfuscator _obfuscator;
        private readonly IDataSigner _signer;
        private readonly ITunnelPacketBuilder<DefaultTunnelPacket> _packetBuilder;
        private readonly IPEndPoint _replyEndpoint;

        private Task? _processRepliesTask;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private bool _disposed = false;
        private object _rootLock = new();

        public DefaultClientSession(ILogger logger,
            ITransportClient sessionClient,
            ITransportClient replyClient,
            IDataObfuscator obfuscator,
            IDataSigner signer,
            ITunnelPacketBuilder<DefaultTunnelPacket> packetBuilder,
            IPEndPoint replyEndpoint)
        {
            _logger = logger;
            _sessionClient = sessionClient;
            _replyClient = replyClient;
            _obfuscator = obfuscator;
            _signer = signer;
            _packetBuilder = packetBuilder;
            _replyEndpoint = replyEndpoint;
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            return await _sessionClient.SendAsync(data, endPoint, cancellationToken);
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
                _processRepliesTask = ProcessAsync(_cts.Token);
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

            await (_processRepliesTask ?? Task.CompletedTask);
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
                _sessionClient.Dispose();
            }
        }

        private async Task ProcessAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _sessionClient.ReceiveAsync(cancellationToken);
                    var data = result.Data;

                    var obfuscatePacket = _obfuscator.Obfuscate(data);
                    var sign = _signer.CreateSignature(obfuscatePacket);

                    var tunnelPacket = new DefaultTunnelPacket
                    {
                        Data = obfuscatePacket,
                        Sign = sign,
                    };

                    var tunnelRawData = _packetBuilder.BuildPacket(tunnelPacket);

                    await _replyClient.SendAsync(
                        data: tunnelRawData,
                        endPoint: _replyEndpoint,
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Processing session replies data error: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
        }
    }
}
