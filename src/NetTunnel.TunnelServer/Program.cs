using NetTunnel.Application.Entities;
using NetTunnel.Application.Interfaces;
using NetTunnel.Domain.Interfaces;
using NetTunnel.Infrastucture;
using NetTunnel.Infrastucture.Processing;
using NetTunnel.Infrastucture.Security;
using NetTunnel.TunnelServer;
using System.Net;
using System.Text;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddTransient<IDataObfuscator, XorDataObfuscator>(sp =>
{
    return new XorDataObfuscator(Encoding.UTF8.GetBytes("secret"));
});
builder.Services.AddTransient<IDataSigner, HmacDataSigner>(sp =>
{
    return new HmacDataSigner(HmacDataSigner.Algorithm.SHA256, Encoding.UTF8.GetBytes("secret"));
});
builder.Services.AddSingleton<ITunnelPacketBuilder<DefaultTunnelPacket>, StreamPacketBuilder>();
builder.Services.AddTransient<ITunnelTransportClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpTransportClient>>();

    var client = new UdpTransportClient(logger,
        new IPEndPoint(IPAddress.Loopback, 5555));

    return client;
});
builder.Services.AddTransient<IExternalTransportClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpTransportClient>>();

    var client = new UdpTransportClient(logger,
        new IPEndPoint(IPAddress.Loopback, 8090));

    return client;
});

builder.Services.AddSingleton<ITunnelNode>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<TunnelServer>>();
    var obfuscator = sp.GetRequiredService<IDataObfuscator>();
    var tunnelSigner = sp.GetRequiredService<IDataSigner>();
    var externalSigner = sp.GetRequiredService<IDataSigner>();
    var packerBuilder = sp.GetRequiredService<ITunnelPacketBuilder<DefaultTunnelPacket>>();
    var tunnelClient = sp.GetRequiredService<ITunnelTransportClient>();
    var externalClient = sp.GetRequiredService<IExternalTransportClient>();

    return new TunnelServer(logger,
        obfuscator,
        tunnelSigner,
        externalSigner,
        packerBuilder,
        tunnelClient,
        new IPEndPoint(IPAddress.Loopback, 5555)
        );
});

builder.Services.AddHostedService<TunnelServerHostingService>();

var host = builder.Build();
host.Run();
