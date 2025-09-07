using NetTunnel.Test;

namespace NetTunnel.UdpEchoServer
{
    internal class EchoServerHostingService : IHostedService
    {
        private readonly ILogger<EchoServerHostingService> _logger;
        private readonly UdpReceiver _receiver;

        public EchoServerHostingService(ILogger<EchoServerHostingService> logger, UdpReceiver receiver)
        {
            _logger = logger;
            _receiver = receiver;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start udp echo server");

            await _receiver.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stop udp echo server");

            await _receiver.StopAsync();
        }
    }
}
