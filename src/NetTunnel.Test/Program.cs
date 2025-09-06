using Microsoft.Extensions.Logging;
using NetTunnel.Core;
using NetTunnel.Test;
using System.Net;

internal class Program
{
    private static async Task Main(string[] args)
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var testKey = "some key";

        var tunnelClient = loggerFactory.CreateLogger<UdpTunnelClient>();
        var tunnelServer = loggerFactory.CreateLogger<UdpTunnelServer>();
        var udpSenderLogger = loggerFactory.CreateLogger<UdpSender>();
        var udpReceiverLogger = loggerFactory.CreateLogger<UdpReceiver>();

        using var client = new UdpTunnelClient(tunnelClient, 8080,
            new IPEndPoint(IPAddress.Loopback, 5555), testKey);

        using var server = new UdpTunnelServer(tunnelServer, loggerFactory,
            new IPEndPoint(IPAddress.Loopback, 5555), 8090, testKey,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10));

        using var udpSender = new UdpSender(udpSenderLogger,
            new IPEndPoint(IPAddress.Loopback, 8080), TimeSpan.FromSeconds(20));

        using var udpReceiver = new UdpReceiver(udpReceiverLogger,
            new IPEndPoint(IPAddress.Loopback, 8090));

        Console.WriteLine("Start tunnel");
        await server.StartAsync();
        await client.StartAsync();

        await Task.Delay(1000);

        Console.WriteLine("Start sender and receiver");
        await udpReceiver.StartAsync();
        await udpSender.StartAsync();

        Console.WriteLine("Wait input");
        Console.ReadLine();

        Console.WriteLine("Stop sender and receiver");
        await udpSender.StopAsync();
        await udpReceiver.StopAsync();

        await Task.Delay(1000);

        Console.WriteLine("Stop tunnel");
        await client.StopAsync();
        await server.StopAsync();
    }
}