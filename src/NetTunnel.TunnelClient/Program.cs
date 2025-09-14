using Microsoft.Extensions.Options;
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

builder.Services.Configure<ClientSettings>(builder.Configuration.GetSection(ClientSettings.Section));

builder.Services.AddTransient<IDataObfuscator, XorDataObfuscator>(sp =>
{
    var clientSettings = sp.GetRequiredService<IOptions<ClientSettings>>().Value;

    return new XorDataObfuscator(Encoding.UTF8.GetBytes(clientSettings.PreSharedKey));
});
builder.Services.AddTransient<IDataSigner, HmacDataSigner>(sp =>
{
    var clientSettings = sp.GetRequiredService<IOptions<ClientSettings>>().Value;

    return new HmacDataSigner(HmacDataSigner.Algorithm.SHA256, Encoding.UTF8.GetBytes(clientSettings.PreSharedKey));
});
builder.Services.AddSingleton<ITunnelPacketBuilder<DefaultTunnelPacket>, StreamPacketBuilder>();
builder.Services.AddTransient<ITunnelTransportClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpTransportClient>>();

    var client = new UdpTransportClient(logger,
        new IPEndPoint(IPAddress.Any, 0));

    return client;
});
builder.Services.AddTransient<IExternalTransportClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpTransportClient>>();
    var clientSettings = sp.GetRequiredService<IOptions<ClientSettings>>().Value;

    var client = new UdpTransportClient(logger,
        new IPEndPoint(clientSettings.GetListenIp, clientSettings.ListenPort));

    return client;
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
    var clientSettings = sp.GetRequiredService<IOptions<ClientSettings>>().Value;

    return new TunnelClient(logger,
        obfuscator,
        tunnelSigner,
        externalSigner,
        packerBuilder,
        tunnelClient,
        externalClient,
        new IPEndPoint(clientSettings.GetServerIp, clientSettings.ServerPort)
        );
});

builder.Services.AddHostedService<TunnelClientHostingService>();

var host = builder.Build();
host.Run();
