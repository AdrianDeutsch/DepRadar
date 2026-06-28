# syntax=docker/dockerfile:1
# Multi-stage build producing two runtime images from one source tree: the Web API
# (api target) and the ingestion Worker (worker target). The Aspire AppHost is not
# built here — only the Api/Worker and what they reference.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/DepRadar.Api/DepRadar.Api.csproj    -c Release -o /app/api
RUN dotnet publish src/DepRadar.Worker/DepRadar.Worker.csproj -c Release -o /app/worker

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
# curl is used by the compose health check that gates the Worker on a migrated database.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/api ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DepRadar.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS worker
WORKDIR /app
COPY --from=build /app/worker ./
ENTRYPOINT ["dotnet", "DepRadar.Worker.dll"]
