using NetTunnel.Application.Entities;
using NetTunnel.Application.Interfaces;
using NetTunnel.Domain.Interfaces;
using NetTunnel.Infrastucture;
using NetTunnel.Infrastucture.Processing;
using NetTunnel.Infrastucture.Security;
using NetTunnel.TunnelClient;
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
builder.Services.AddSingleton<ITunnelPacketBuilder<DefaultTunnelPacket>, DefaultPacketBuilder>();
builder.Services.AddTransient(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpTransportClient>>();

    var client = new UdpTransportClient(logger,
        new IPEndPoint(IPAddress.Any, 0));

    return (ITunnelTransportClient)client;
});
builder.Services.AddTransient(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpTransportClient>>();

    var client = new UdpTransportClient(logger,
        new IPEndPoint(IPAddress.Loopback, 8080));

    return (IExternalTransportClient)client;
});
builder.Services.AddSingleton<ITunnelNode>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<TunnelClient>>();
    var obfuscator = sp.GetRequiredService<IDataObfuscator>();
    var tunnelSigner = sp.GetRequiredService<IDataSigner>();
    var externalSigner = sp.GetRequiredService<IDataSigner>();
    var packerBuilder = sp.GetRequiredService<ITunnelPacketBuilder<DefaultTunnelPacket>>();
    var tunnelClient = sp.GetRequiredService<ITunnelTransportClient>();
    var externalClient = sp.GetRequiredService<IExternalTransportClient>();

    return new TunnelClient(logger,
        obfuscator,
        tunnelSigner,
        externalSigner,
        packerBuilder,
        tunnelClient,
        externalClient,
        new IPEndPoint(IPAddress.Loopback, 5555)
        );
});

builder.Services.AddHostedService<TunnelClientHostingService>();

var host = builder.Build();
host.Run();
