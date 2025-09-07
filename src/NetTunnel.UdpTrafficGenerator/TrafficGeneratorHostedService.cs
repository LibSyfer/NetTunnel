
namespace NetTunnel.UdpTrafficGenerator
{
    internal class TrafficGeneratorHostedService : IHostedService
    {
        private readonly ILogger<TrafficGeneratorHostedService> _logger;
        private readonly UdpSender _udpSender;

        public TrafficGeneratorHostedService(ILogger<TrafficGeneratorHostedService> logger, UdpSender udpSender)
        {
            _logger = logger;
            _udpSender = udpSender;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start udp traffic generating");

            await _udpSender.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stop udp traffic generating");

            await _udpSender.StopAsync();
        }
    }
}
