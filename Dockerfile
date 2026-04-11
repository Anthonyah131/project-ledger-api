# ─────────────────────────────────────────────────────────────
# STAGE 1 — build
# Usamos la imagen SDK completa: tiene el compilador, NuGet, etc.
# Esta imagen es pesada (~900 MB) pero SOLO se usa para compilar.
# El resultado final NO incluye el SDK.
# ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiamos primero solo los archivos de proyecto para aprovechar
# el cache de capas de Docker. Si no tocas el .csproj, Docker
# reutiliza la capa de "restore" sin bajar paquetes de nuevo.
COPY ProjectLedger.API.csproj ./
RUN dotnet restore ProjectLedger.API.csproj

# Ahora copiamos el resto del código fuente y compilamos.
COPY . .
RUN dotnet publish ProjectLedger.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ─────────────────────────────────────────────────────────────
# STAGE 2 — runtime
# Imagen mínima: solo el runtime de ASP.NET Core (~220 MB).
# No tiene compilador, no tiene NuGet. Más segura y liviana.
# ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# La imagen aspnet slim no incluye curl. Lo instalamos solo para el
# health check. --no-install-recommends evita paquetes innecesarios.
# rm -rf /var/lib/apt/lists/* limpia el cache de apt para reducir el tamaño.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copiamos únicamente la salida del publish del stage anterior.
COPY --from=build /app/publish .

# Puerto que expone la aplicación dentro del contenedor.
# Debe coincidir con lo que configures en docker-compose.yml.
EXPOSE 8080

# Health check a nivel de imagen.
# Docker ejecuta este comando cada 30s. Si falla 3 veces seguidas,
# el contenedor pasa a estado "unhealthy" y puede ser reiniciado.
#
# --interval:     cada cuánto chequear
# --timeout:      tiempo máximo que tiene el comando para responder
# --start-period: gracia inicial (la API tarda en arrancar)
# --retries:      cuántas fallas consecutivas antes de "unhealthy"
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Punto de entrada: el ejecutable generado por dotnet publish.
ENTRYPOINT ["dotnet", "ProjectLedger.API.dll"]
