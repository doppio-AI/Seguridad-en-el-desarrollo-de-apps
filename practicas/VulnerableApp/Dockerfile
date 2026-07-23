# Dockerfile para VulnerableApp (.NET 10)
# Laboratorio educativo de pentesting - OWASP

# === STAGE 1: Build ===
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copiar archivo de proyecto y restaurar dependencias (capa cacheable)
COPY VulnerableApp.csproj .
RUN dotnet restore "VulnerableApp.csproj"

# Copiar el resto del codigo y publicar
COPY . .
RUN dotnet publish "VulnerableApp.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore

# === STAGE 2: Runtime ===
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Variables de entorno para la aplicacion
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Docker

# Copiar binarios publicados desde stage de build
COPY --from=build /app/publish .

# Exponer puerto (documentativo, Docker no lo usa directamente)
EXPOSE 8080

# Punto de entrada
ENTRYPOINT ["dotnet", "VulnerableApp.dll"]

