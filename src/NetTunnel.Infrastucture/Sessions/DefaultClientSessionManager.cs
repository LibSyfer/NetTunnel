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
        private readonly ILogger<DefaultClientSessionManager> _logger;
        private readonly IServiceProvider _serviceProvider;

        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _sessionTimeout;

        private readonly ConcurrentDictionary<IPEndPoint, IClientSession> _sessions = new();
        
        public DefaultClientSessionManager(ILogger<DefaultClientSessionManager> logger, IServiceProvider serviceProvider, TimeSpan sessionTimeout, TimeSpan cleanupIntervar)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _sessionTimeout = sessionTimeout;

            _cleanupTimer = new Timer(_ => CleanupInactiveSessions(), null,
                cleanupIntervar, cleanupIntervar);
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint remoteEndPoint, IPEndPoint targetEndPoint, CancellationToken cancellationToken)
        {
            var session = _sessions.GetOrAdd(remoteEndPoint, _ =>
            {
                _logger.LogInformation("Create new session: {RemoteEndPoint} -> {TargetEndpoint}", remoteEndPoint, targetEndPoint);

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

            _logger.LogInformation($"Cleanup {inactiveSessions.Count} inactive sessions");
        }

        public void Dispose()
        {
            foreach (var session in _sessions)
            {
                if (_sessions.TryRemove(session.Key, out var removedSession))
                {
                    removedSession.Dispose();
                }
            }

            _cleanupTimer.Dispose();
        }
    }
}
