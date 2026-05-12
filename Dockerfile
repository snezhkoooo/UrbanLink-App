# ── build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY UrbanLinkStarter/UrbanLinkStarter.csproj UrbanLinkStarter/
RUN dotnet restore UrbanLinkStarter/UrbanLinkStarter.csproj

COPY UrbanLinkStarter/ UrbanLinkStarter/
RUN dotnet publish UrbanLinkStarter/UrbanLinkStarter.csproj \
    -c Release -o /app/publish --no-restore

# ── runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Non-root user for security
RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "UrbanLinkStarter.dll"]
