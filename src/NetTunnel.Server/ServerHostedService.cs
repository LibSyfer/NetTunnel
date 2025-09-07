using NetTunnel.Core;

namespace NetTunnel.Server
{
    public class ServerHostedService : IHostedService
    {
        private readonly ILogger<ServerHostedService> _logger;
        private readonly UdpTunnelServer _server;

        public ServerHostedService(ILogger<ServerHostedService> logger, UdpTunnelServer server)
        {
            _logger = logger;
            _server = server;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start udp tunnel server");

            await _server.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stop udp tunnel server");

            await _server.StopAsync();
        }
    }
}
