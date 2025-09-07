using Microsoft.Extensions.Options;
using NetTunnel.UdpTrafficGenerator;
using System.Net;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<TraficGeneratorSettings>(builder.Configuration.GetSection(TraficGeneratorSettings.Section));
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<UdpSender>>();
    var trafficGeneratorSettings = sp.GetRequiredService<IOptions<TraficGeneratorSettings>>().Value;

    return new UdpSender(logger,
        new IPEndPoint(trafficGeneratorSettings.GetTargetIp, trafficGeneratorSettings.TargetPort),
        TimeSpan.FromMilliseconds(trafficGeneratorSettings.SendingDelayMs),
        trafficGeneratorSettings.SendingMessage);
});

builder.Services.AddHostedService<TrafficGeneratorHostedService>();

var host = builder.Build();
host.Run();
