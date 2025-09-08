using Microsoft.Extensions.Options;
using NetTunnel.Client;
using NetTunnel.Core;
using System.Net;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ClientSettings>(builder.Configuration.GetSection(ClientSettings.Section));
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpTunnelClient>>();
    var clientSettings = sp.GetRequiredService<IOptions<ClientSettings>>().Value;

    var ip = clientSettings.GetServerIp;

    return new UdpTunnelClient(logger,
        new IPEndPoint(clientSettings.GetListenIp, clientSettings.ListenPort),
        new IPEndPoint(clientSettings.GetServerIp, clientSettings.ServerPort),
        clientSettings.PreSharedKey);
});

builder.Services.AddHostedService<ClientHostedService>();

var host = builder.Build();
host.Run();
