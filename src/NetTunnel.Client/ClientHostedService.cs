
using NetTunnel.Core;

namespace NetTunnel.Client
{
    public class ClientHostedService : IHostedService
    {
        private readonly ILogger<ClientHostedService> _logger;
        private readonly UdpTunnelClient _client;

        public ClientHostedService(ILogger<ClientHostedService> logger, UdpTunnelClient client)
        {
            _logger = logger;
            _client = client;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start udp tunnel client");

            await _client.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stop udp tunnel client");

            await _client.StopAsync();
        }
    }
}
