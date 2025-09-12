using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetTunnel.Application.Entities;
using NetTunnel.Application.Interfaces;
using NetTunnel.Application.Interfaces.Sessions;
using NetTunnel.Domain.Interfaces;
using System.Collections.Concurrent;
using System.Net;

namespace NetTunnel.Infrastucture.Sessions
{
    public class DefaultClientSessionManager : IClientSessionManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _sessionTimeout;
        private readonly ConcurrentDictionary<IPEndPoint, IClientSession> _sessions = new();
        
        public DefaultClientSessionManager(IServiceProvider serviceProvider, TimeSpan sessionTimeout)
        {
            _serviceProvider = serviceProvider;
            _sessionTimeout = sessionTimeout;
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint remoteEndPoint, IPEndPoint targetEndPoint, CancellationToken cancellationToken)
        {
            var session = _sessions.GetOrAdd(remoteEndPoint, _ =>
            {
                var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger($"{typeof(DefaultClientSession).FullName}-Session: {remoteEndPoint} -> {targetEndPoint}");

                var sessionClient = _serviceProvider.GetRequiredService<IExternalTransportClient>();
                var tunnelClient = _serviceProvider.GetRequiredService<ITunnelTransportClient>();
                var obfuscator = _serviceProvider.GetRequiredService<IDataObfuscator>();
                var signer = _serviceProvider.GetRequiredService<IDataSigner>();
                var packerBuilder = _serviceProvider.GetRequiredService<ITunnelPacketBuilder<DefaultTunnelPacket>>();

                return new DefaultClientSession(logger,
                    sessionClient,
                    tunnelClient,
                    obfuscator,
                    signer,
                    packerBuilder,
                    remoteEndPoint);
            });

            return await session.SendAsync(
                data: data,
                targetEndPoint: targetEndPoint,
                cancellationToken: cancellationToken);

        }

        public void CleanupInactiveSessions()
        {
            var now = DateTime.UtcNow;
            var inactiveSessions = _sessions
                .Where(kvp => now - kvp.Value.LastActivity > _sessionTimeout)
                .ToList();

            foreach (var session in inactiveSessions)
            {
                if (_sessions.TryRemove(session.Key, out var removedSession))
                {
                    removedSession.Dispose();
                }
            }
        }
    }
}
