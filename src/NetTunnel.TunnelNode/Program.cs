using Microsoft.Extensions.Options;
using NetTunnel.Application.Interfaces;
using NetTunnel.Infrastucture;
using NetTunnel.Infrastucture.Processing;
using NetTunnel.TunnelNode;
using System.Net;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<TunnelNodeSettings>(builder.Configuration.GetSection(TunnelNodeSettings.Section));

builder.Services.AddSingleton<IExternalTransportClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpTransportClient>>();
    var settings = sp.GetRequiredService<IOptions<TunnelNodeSettings>>().Value;

    var client = new UdpTransportClient(
        logger,
        new IPEndPoint(settings.GetExternalListenIp, settings.ExternalListenPort)
        );

    return client;
});

builder.Services.AddSingleton<ITunnelTransportClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpTransportClient>>();
    var settings = sp.GetRequiredService<IOptions<TunnelNodeSettings>>().Value;

    var client = new UdpTransportClient(
        logger,
        new IPEndPoint(settings.GetTunnelListenIp, settings.TunnelListenPort)
        );

    return client;
});

builder.Services.AddSingleton<ITunnelNode>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<TunnelNode>>();
    var externalClient = sp.GetRequiredService<IExternalTransportClient>();
    var tunnelClient = sp.GetRequiredService<ITunnelTransportClient>();
    var settings = sp.GetRequiredService<IOptions<TunnelNodeSettings>>().Value;

    return new TunnelNode(
        logger,
        externalClient,
        tunnelClient,
        new IPEndPoint(settings.GetExternalTargetIp, settings.ExternalTargetPort),
        new IPEndPoint(settings.GetTunnelTargetIp, settings.TunnelTargetPort)
        );
});

builder.Services.AddHostedService<TunnelNodeHostingService>();

var host = builder.Build();
host.Run();
