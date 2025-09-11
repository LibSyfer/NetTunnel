namespace NetTunnel.Application.Interfaces
{
    public interface IExternalClientSession<TTransportClient> : IDisposable
        where TTransportClient : class
    {
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
