using Microsoft.Extensions.Logging;
using NetTunnel.Domain.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace NetTunnel.Infrastucture
{
    public class UdpTunnelClient : ITunnelNode, IDisposable
    {
        private readonly ILogger<UdpTunnelClient> _logger;
        private readonly IDataObfuscator _obfuscator;
        private readonly IDataSigner _signer;

        private readonly UdpClient _listenClient;
        private readonly UdpClient _forwardClient;
        private readonly IPEndPoint _serverEndpoint;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private bool _disposed = false;
        private object _rootLock = new();

        public UdpTunnelClient(ILogger<UdpTunnelClient> logger,
            IDataObfuscator obfuscator,
            IDataSigner signer,
            IPEndPoint listenEndpoint,
            IPEndPoint serverEndpoint)
        {
            _logger = logger;
            _obfuscator = obfuscator;
            _signer = signer;

            _listenClient = new UdpClient(listenEndpoint);
            _forwardClient = new UdpClient(0);
            _serverEndpoint = serverEndpoint;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning || _disposed) return Task.CompletedTask;

            lock (_rootLock)
            {
                if (_isRunning || _disposed) return Task.CompletedTask;
                _isRunning = true;

                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

            try
            {
                // start handling
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Start error: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isRunning) return;

            lock (_rootLock)
            {
                if (!_isRunning) return;
                _isRunning = false;

                _cts?.Cancel();
            }

            await Task.WhenAll();
        }

        public void Dispose()
        {
            if (_disposed) return;

            lock(_rootLock)
            {
                if (_disposed) return;
                _disposed = true;

                _cts?.Cancel();
                _listenClient.Close();
                _forwardClient.Close();
                _listenClient.Dispose();
                _forwardClient.Dispose();
            }
        }
    }
}
