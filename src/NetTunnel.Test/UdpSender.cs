using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace NetTunnel.Test
{
    internal class UdpSender : IDisposable
    {
        private readonly ILogger<UdpSender> _logger;
        private readonly UdpClient _client;
        private readonly IPEndPoint _targetEndpoint;

        private Task? _processingRequestsTask, _processingRepliesTask;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private bool _isDisposed = false;
        private object _lock = new object();

        public UdpSender(ILogger<UdpSender> logger, IPEndPoint targetEndpoint)
        {
            _logger = logger;
            _client = new UdpClient(0);
            _targetEndpoint = targetEndpoint;
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
                _processingRepliesTask = ProcessRepliesAsync(_cts.Token);
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

            await Task.WhenAll(_processingRequestsTask!, _processingRepliesTask!);
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
            int index = 1;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var message = "Some data message";

                    var data = Encoding.UTF8.GetBytes(message + $" {index}");

                    _logger.LogInformation("Send {DataLength}bytes to {TargetEndpoint}", data.Length, _targetEndpoint);
                    await _client.SendAsync(new ReadOnlyMemory<byte>(data), _targetEndpoint, cancellationToken);

                    await Task.Delay(5000, cancellationToken);
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

        private async Task ProcessRepliesAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _client.ReceiveAsync(cancellationToken);
                    var message = Encoding.UTF8.GetString(result.Buffer);

                    _logger.LogInformation($"Reply message: {message}");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Processing replies error: {ErrorMessage}", ex.Message);
                    break;
                }
            }
        }
    }
}
