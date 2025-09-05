using Microsoft.Extensions.Logging;
using NetTunnel.Core;
using System.Net;

internal class Program
{
    private static async Task Main(string[] args)
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var testKey = "some key";

        var tunnelClient = loggerFactory.CreateLogger<UdpTunnelClient>();
        var tunnelServer = loggerFactory.CreateLogger<UdpTunnelServer>();

        using var client = new UdpTunnelClient(tunnelClient, 8080,
            new IPEndPoint(IPAddress.Loopback, 5555), testKey);

        using var server = new UdpTunnelServer(tunnelServer, loggerFactory,
            new IPEndPoint(IPAddress.Loopback, 5555), 8090, testKey);

        Console.WriteLine("Start server");
        await server.StartAsync();
        Console.WriteLine("Start client");
        await client.StartAsync();

        Console.WriteLine("Wait input");
        Console.ReadLine();

        Console.WriteLine("Stop client");
        await client.StopAsync();
        Console.WriteLine("Stop server");
        await server.StopAsync();

        Console.WriteLine("Dispose");
        client.Dispose();
        server.Dispose();

        Console.WriteLine("Start server");
        await server.StartAsync();
        Console.WriteLine("Start client");
        await client.StartAsync();

        Console.WriteLine("Wait input");
        Console.ReadLine();
    }
}