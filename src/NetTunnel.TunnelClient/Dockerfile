# См. статью по ссылке https://aka.ms/customizecontainer, чтобы узнать как настроить контейнер отладки и как Visual Studio использует этот Dockerfile для создания образов для ускорения отладки.

# Этот этап используется при запуске из VS в быстром режиме (по умолчанию для конфигурации отладки)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app


# Этот этап используется для сборки проекта службы
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/NetTunnel.TunnelClient/NetTunnel.TunnelClient.csproj", "src/NetTunnel.TunnelClient/"]
COPY ["src/NetTunnel.Application/NetTunnel.Application.csproj", "src/NetTunnel.Application/"]
COPY ["src/NetTunnel.Domain/NetTunnel.Domain.csproj", "src/NetTunnel.Domain/"]
COPY ["src/NetTunnel.Infrastucture/NetTunnel.Infrastucture.csproj", "src/NetTunnel.Infrastucture/"]
RUN dotnet restore "./src/NetTunnel.TunnelClient/NetTunnel.TunnelClient.csproj"
COPY . .
WORKDIR "/src/src/NetTunnel.TunnelClient"
RUN dotnet build "./NetTunnel.TunnelClient.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Этот этап используется для публикации проекта службы, который будет скопирован на последний этап
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./NetTunnel.TunnelClient.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Этот этап используется в рабочей среде или при запуске из VS в обычном режиме (по умолчанию, когда конфигурация отладки не используется)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NetTunnel.TunnelClient.dll"]