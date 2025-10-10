using Microsoft.Extensions.Options;
using NetTunnel.UdpTrafficGenerator;
using System.Net;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<TraficGeneratorSettings>(builder.Configuration.GetSection(TraficGeneratorSettings.Section));
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpSender>>();
    var settings = sp.GetRequiredService<IOptions<TraficGeneratorSettings>>().Value;

    return new UdpSender(logger,
        new IPEndPoint(settings.GetListenIp, settings.ListenPort),
        new IPEndPoint(settings.GetTargetIp, settings.TargetPort),
        TimeSpan.FromMilliseconds(settings.SendingDelayMs),
        settings.SendingMessage);
});

builder.Services.AddHostedService<TrafficGeneratorHostedService>();

var host = builder.Build();
host.Run();
