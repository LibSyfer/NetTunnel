using Microsoft.Extensions.Options;
using NetTunnel.Application.Interfaces;
using NetTunnel.Domain.Interfaces;
using NetTunnel.Infrastucture;
using NetTunnel.Infrastucture.Processing;
using NetTunnel.Infrastucture.Security;
using NetTunnel.TunnelNode;
using System.Net;
using System.Text;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<TunnelNodeSettings>(builder.Configuration.GetSection(TunnelNodeSettings.Section));

builder.Services.AddSingleton<IExternalTransportClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<TunnelNodeSettings>>().Value;

    var client = new UdpTransportClient(
        new IPEndPoint(settings.GetExternalListenIp, settings.ExternalListenPort)
        );

    return client;
});

builder.Services.AddSingleton<ITunnelTransportClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<TunnelNodeSettings>>().Value;

    var client = new UdpTransportClient(
        new IPEndPoint(settings.GetTunnelListenIp, settings.TunnelListenPort)
        );

    return client;
});

builder.Services.AddSingleton<IDataObfuscator>(sp =>
{
    return new XorDataObfuscator(Encoding.UTF8.GetBytes("secret"));
});

builder.Services.AddSingleton<ITunnelNode>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<TunnelNode>>();
    var externalClient = sp.GetRequiredService<IExternalTransportClient>();
    var tunnelClient = sp.GetRequiredService<ITunnelTransportClient>();
    var obfuscator = sp.GetRequiredService<IDataObfuscator>();
    var settings = sp.GetRequiredService<IOptions<TunnelNodeSettings>>().Value;

    return new TunnelNode(
        logger,
        externalClient,
        tunnelClient,
        obfuscator,
        new IPEndPoint(settings.GetExternalTargetIp, settings.ExternalTargetPort),
        new IPEndPoint(settings.GetTunnelTargetIp, settings.TunnelTargetPort)
        );
});

builder.Services.AddHostedService<TunnelNodeHostingService>();

var host = builder.Build();
host.Run();
