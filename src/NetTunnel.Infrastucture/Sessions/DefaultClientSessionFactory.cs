using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetTunnel.Application.Entities;
using NetTunnel.Application.Interfaces;
using NetTunnel.Application.Interfaces.Sessions;
using NetTunnel.Domain.Interfaces;
using System.Net;

namespace NetTunnel.Infrastucture.Sessions
{
    public class DefaultClientSessionFactory<T> : IClientSessionFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServiceProvider _serviceProvider;

        public DefaultClientSessionFactory(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        {
            _loggerFactory = loggerFactory;
            _serviceProvider = serviceProvider;
        }

        public IClientSession CreateSession(ITransportClient _replyClient, IPEndPoint replyEndpoint)
        {
            var logger = _loggerFactory.CreateLogger($"{typeof(DefaultClientSession)}-Session: {replyEndpoint}");
            var sessionClient = _serviceProvider.GetRequiredService<IExternalTransportClient>();
            var obfuscator = _serviceProvider.GetRequiredService<IDataObfuscator>();
            var signer = _serviceProvider.GetRequiredService<IDataSigner>();
            var packerBuilder = _serviceProvider.GetRequiredService<ITunnelPacketBuilder<DefaultTunnelPacket>>();

            return new DefaultClientSession(logger,
                sessionClient,
                _replyClient,
                obfuscator,
                signer,
                packerBuilder,
                replyEndpoint);
        }
    }
}
