using Microsoft.Extensions.Options;
using NetTunnel.Core;
using NetTunnel.Server;
using System.Net;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ServerSettings>(builder.Configuration.GetSection(ServerSettings.Section));
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpTunnelServer>>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var serverSettings = sp.GetRequiredService<IOptions<ServerSettings>>().Value;

    return new UdpTunnelServer(logger, loggerFactory,
        new IPEndPoint(serverSettings.GetListenIp, serverSettings.ListenPort),
        serverSettings.TargetPort,
        serverSettings.PreSharedKey,
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(1));
});

builder.Services.AddHostedService<ServerHostedService>();

var host = builder.Build();
host.Run();
