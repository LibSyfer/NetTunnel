using NetTunnel.Application.Interfaces;

namespace NetTunnel.TunnelNode
{
    internal class TunnelNodeHostingService : IHostedService
    {
        private readonly ILogger<TunnelNodeHostingService> _logger;
        private readonly ITunnelNode _client;

        public TunnelNodeHostingService(ILogger<TunnelNodeHostingService> logger, ITunnelNode client)
        {
            _logger = logger;
            _client = client;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start tunnel client");

            await _client.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stop tunnel client");

            await _client.StopAsync(cancellationToken);
        }
    }
}
