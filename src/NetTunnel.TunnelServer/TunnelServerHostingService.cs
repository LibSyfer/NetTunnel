using NetTunnel.Application.Interfaces;

namespace NetTunnel.TunnelServer
{
    internal class TunnelServerHostingService : IHostedService
    {
        private readonly ILogger<TunnelServerHostingService> _logger;
        private readonly ITunnelNode _client;

        public TunnelServerHostingService(ILogger<TunnelServerHostingService> logger, ITunnelNode client)
        {
            _logger = logger;
            _client = client;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start tunnel server");

            await _client.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stop tunnel server");

            await _client.StopAsync(cancellationToken);
        }
    }
}
