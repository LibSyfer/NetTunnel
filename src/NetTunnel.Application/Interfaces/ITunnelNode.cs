namespace NetTunnel.Application.Interfaces
{
    public interface ITunnelNode
    {
        Task StartAsync(CancellationToken cancellationToken = default);

        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
