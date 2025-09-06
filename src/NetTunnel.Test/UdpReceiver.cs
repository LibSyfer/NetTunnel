using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetTunnel.Test
{
    internal class UdpReceiver : IDisposable
    {
        private readonly ILogger<UdpReceiver> _logger;
        private readonly UdpClient _client;

        private Task? _processingRequestsTask;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private bool _isDisposed = false;
        private object _lock = new object();

        public UdpReceiver(ILogger<UdpReceiver> logger, IPEndPoint listenEndpoint)
        {
            _logger = logger;
            _client = new UdpClient(listenEndpoint);
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isRunning) return Task.CompletedTask;

            lock (_lock)
            {
                if (_isRunning) return Task.CompletedTask;
                _isRunning = true;

                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

            try
            {
                _processingRequestsTask = ProcessRequestsAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Starting error: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (!_isRunning) return;

            lock (_lock)
            {
                if (!_isRunning) return;
                _isRunning = false;

                _cts?.Cancel();
            }

            await Task.WhenAll(_processingRequestsTask!);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            lock (_lock)
            {
                if (_isDisposed) return;
                _isDisposed = true;

                _cts?.Dispose();
                _client.Dispose();
            }
        }

        private async Task ProcessRequestsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _client.ReceiveAsync(cancellationToken);
                    var message = Encoding.UTF8.GetString(result.Buffer);

                    _logger.LogInformation($"Receive request message: {message}");

                    await _client.SendAsync(new ReadOnlyMemory<byte>(result.Buffer), result.RemoteEndPoint, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Processing requests error: {ErrorMessage}", ex.Message);
                    break;
                }
            }
        }
    }
}
