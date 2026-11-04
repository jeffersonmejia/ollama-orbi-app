# Build and publish the ASP.NET Core host, including the Blazor WebAssembly client.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Core/Presentation/Presentation.csproj
RUN dotnet publish src/Core/Presentation/Presentation.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends postgresql-client \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/.logs /var/orbi/keys /backups \
    && chown -R app:app /app/.logs /var/orbi /backups
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "OrbiApp.Presentation.dll"]
