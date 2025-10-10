using Microsoft.Extensions.Options;
using NetTunnel.Test;
using NetTunnel.UdpEchoServer;
using System.Net;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<UdpEchoServerSettings>(builder.Configuration.GetSection(UdpEchoServerSettings.Section));
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpReceiver>>();
    var serverSettings = sp.GetRequiredService<IOptions<UdpEchoServerSettings>>().Value;

    return new UdpReceiver(logger, new IPEndPoint(serverSettings.GetListenIp, serverSettings.ListenPort));
});

builder.Services.AddHostedService<EchoServerHostingService>();

var host = builder.Build();
host.Run();
