using NetTunnel.Application.Interfaces;

namespace NetTunnel.TunnelClient
{
    public class TunnelClientHostingService : IHostedService
    {
        private readonly ILogger<TunnelClientHostingService> _logger;
        private readonly ITunnelNode _client;

        public TunnelClientHostingService(ILogger<TunnelClientHostingService> logger, ITunnelNode client)
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
