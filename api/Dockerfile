FROM docker:cli AS docker-cli

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY BDeployer.Api.csproj NuGet.Config ./
RUN dotnet restore BDeployer.Api.csproj --configfile NuGet.Config

COPY . .
RUN dotnet publish BDeployer.Api.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG BDEPLOYER_UID=10001
ARG BDEPLOYER_GID=10001
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl git openssh-client \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid ${BDEPLOYER_GID} bdeployer \
    && useradd --uid ${BDEPLOYER_UID} --gid ${BDEPLOYER_GID} --create-home --shell /bin/bash bdeployer \
    && mkdir -p /app/data /opt/bdeployer/projects \
    && chown -R bdeployer:bdeployer /app /opt/bdeployer
COPY --from=docker-cli /usr/local/bin/docker /usr/local/bin/docker
COPY --from=docker-cli /usr/local/libexec/docker/cli-plugins/docker-compose /usr/local/libexec/docker/cli-plugins/docker-compose
COPY --from=build --chown=bdeployer:bdeployer /app/publish .
USER bdeployer
EXPOSE 8080
ENTRYPOINT ["dotnet", "BDeployer.Api.dll"]
