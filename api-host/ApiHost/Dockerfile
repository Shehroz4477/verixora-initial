FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore api-host/ApiHost/ApiHost.csproj --disable-parallel
RUN dotnet publish api-host/ApiHost/ApiHost.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN groupadd --gid 10001 verixora \
    && useradd --uid 10001 --gid verixora --create-home --shell /usr/sbin/nologin verixora

COPY --from=build --chown=verixora:verixora /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

USER verixora
ENTRYPOINT ["dotnet", "ApiHost.dll"]
