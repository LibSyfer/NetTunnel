using Microsoft.Extensions.Options;
using NetTunnel.Application.Entities;
using NetTunnel.Application.Interfaces;
using NetTunnel.Application.Interfaces.Sessions;
using NetTunnel.Domain.Interfaces;
using NetTunnel.Infrastucture;
using NetTunnel.Infrastucture.Processing;
using NetTunnel.Infrastucture.Security;
using NetTunnel.Infrastucture.Sessions;
using NetTunnel.TunnelServer;
using System.Net;
using System.Text;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ServerSettings>(builder.Configuration.GetSection(ServerSettings.Section));

builder.Services.AddTransient<IDataObfuscator, XorDataObfuscator>(sp =>
{
    var serverSettings = sp.GetRequiredService<IOptions<ServerSettings>>().Value;

    return new XorDataObfuscator(Encoding.UTF8.GetBytes(serverSettings.PreSharedKey));
});
builder.Services.AddTransient<IDataSigner, HmacDataSigner>(sp =>
{
    var serverSettings = sp.GetRequiredService<IOptions<ServerSettings>>().Value;

    return new HmacDataSigner(HmacDataSigner.Algorithm.SHA256, Encoding.UTF8.GetBytes(serverSettings.PreSharedKey));
});
builder.Services.AddSingleton<ITunnelPacketBuilder<DefaultTunnelPacket>, StreamPacketBuilder>();
builder.Services.AddSingleton<ITunnelTransportClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpTransportClient>>();
    var serverSettings = sp.GetRequiredService<IOptions<ServerSettings>>().Value;

    var client = new UdpTransportClient(logger,
        new IPEndPoint(serverSettings.GetListenIp, serverSettings.ListenPort));

    return client;
});
builder.Services.AddTransient<IExternalTransportClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpTransportClient>>();

    var client = new UdpTransportClient(logger,
        new IPEndPoint(IPAddress.Any, 0));

    return client;
});
builder.Services.AddSingleton<IClientSessionManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DefaultClientSessionManager>>();

    return new DefaultClientSessionManager(logger,
        sp,
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(2)
        );
});

builder.Services.AddSingleton<ITunnelNode>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<TunnelServer>>();
    var obfuscator = sp.GetRequiredService<IDataObfuscator>();
    var tunnelSigner = sp.GetRequiredService<IDataSigner>();
    var packerBuilder = sp.GetRequiredService<ITunnelPacketBuilder<DefaultTunnelPacket>>();
    var tunnelClient = sp.GetRequiredService<ITunnelTransportClient>();
    var sessionManager = sp.GetRequiredService<IClientSessionManager>();
    var serverSettings = sp.GetRequiredService<IOptions<ServerSettings>>().Value;

    return new TunnelServer(logger,
        obfuscator,
        tunnelSigner,
        packerBuilder,
        tunnelClient,
        sessionManager,
        new IPEndPoint(serverSettings.GetTargetIp, serverSettings.TargetPort));
});

builder.Services.AddHostedService<TunnelServerHostingService>();

var host = builder.Build();
host.Run();
