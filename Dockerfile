# Etapa 1: compilación de Orbi App con .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copiar el archivo del proyecto y restaurar dependencias
COPY ["src/SakilaApp.csproj", "src/"]
RUN dotnet restore "src/SakilaApp.csproj"

# Copiar el resto del proyecto
COPY . .

# Publicar la aplicación para la imagen utilizada por Docker Stack
RUN dotnet publish "src/SakilaApp.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "SakilaApp.dll"]
