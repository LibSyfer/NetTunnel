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
        private DateTime _lastActivity;

        private CancellationTokenSource _cts;

        public DateTime LastActivity => _lastActivity;

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

            _cts = new CancellationTokenSource();
            _ = ProcessAsync(_cts.Token);
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            _lastActivity = DateTime.UtcNow;
            return await _sessionClient.SendAsync(data, endPoint, cancellationToken);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _sessionClient.Dispose();
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

                    _lastActivity = DateTime.UtcNow;
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
