# syntax=docker/dockerfile:1
# Imágenes de FlowRide (ADR-0010): API del operador, borde común y herramienta de aprovisionamiento, todas con la
# misma versión. Se construye una por destino:
#   docker build --target api -t flowride-api:<versión>-<sha> .
#   docker build --target borde -t flowride-borde:<versión>-<sha> .
#   docker build --target aprovisionamiento -t flowride-aprovisionamiento:<versión>-<sha> .
# Versiones fijas de las imágenes base. Las de ejecución son chiseled (sin shell ni gestor de paquetes, usuario sin
# privilegios) en su variante "extra", que trae ICU y la base de zonas horarias que necesitan es-CO y ADR-0020.
ARG SDK=mcr.microsoft.com/dotnet/sdk:10.0.403-noble
ARG ASPNET=mcr.microsoft.com/dotnet/aspnet:10.0.14-noble-chiseled-extra
ARG RUNTIME=mcr.microsoft.com/dotnet/runtime:10.0.14-noble-chiseled-extra

FROM ${SDK} AS compilacion
WORKDIR /src

# Primero solo los proyectos, para que la restauración quede en caché mientras no cambien las dependencias.
COPY Directory.Build.props dotnet-tools.json ./
COPY src/FlowRide.Domain/FlowRide.Domain.csproj src/FlowRide.Domain/
COPY src/FlowRide.Application/FlowRide.Application.csproj src/FlowRide.Application/
COPY src/FlowRide.Infrastructure/FlowRide.Infrastructure.csproj src/FlowRide.Infrastructure/
COPY src/FlowRide.API/FlowRide.API.csproj src/FlowRide.API/
COPY src/FlowRide.Borde/FlowRide.Borde.csproj src/FlowRide.Borde/
COPY src/FlowRide.Aprovisionamiento/FlowRide.Aprovisionamiento.csproj src/FlowRide.Aprovisionamiento/
RUN dotnet restore src/FlowRide.API/FlowRide.API.csproj \
 && dotnet restore src/FlowRide.Borde/FlowRide.Borde.csproj \
 && dotnet restore src/FlowRide.Aprovisionamiento/FlowRide.Aprovisionamiento.csproj \
 && dotnet tool restore

COPY src/ src/
RUN dotnet publish src/FlowRide.API/FlowRide.API.csproj -c Release --no-restore -o /publicado/api \
 && dotnet publish src/FlowRide.Borde/FlowRide.Borde.csproj -c Release --no-restore -o /publicado/borde \
 && dotnet publish src/FlowRide.Aprovisionamiento/FlowRide.Aprovisionamiento.csproj -c Release --no-restore -o /publicado/aprovisionamiento

# Migration bundles de EF Core (ADR-0004): ejecutables autocontenidos que aplican las migraciones pendientes del Silo
# y del catálogo; viajan con la misma versión que la API. Etapa aparte: solo la necesita la imagen de aprovisionamiento.
FROM compilacion AS migraciones
RUN dotnet restore src/FlowRide.Infrastructure/FlowRide.Infrastructure.csproj --runtime linux-x64 \
 && for contexto in FlowRideDbContext:silo CatalogoDbContext:catalogo; do \
      dotnet ef migrations bundle \
        --project src/FlowRide.Infrastructure/FlowRide.Infrastructure.csproj \
        --startup-project src/FlowRide.Infrastructure/FlowRide.Infrastructure.csproj \
        --context "${contexto%%:*}" --configuration Release --runtime linux-x64 --self-contained \
        --output "/publicado/migraciones/efbundle-${contexto##*:}" || exit 1; \
    done

# Imágenes finales. Etiquetas OCI con la versión y el commit, que pasa el CI.
FROM ${ASPNET} AS api
ARG VERSION=0.0.0-local
ARG REVISION=local
LABEL org.opencontainers.image.title="flowride-api" \
      org.opencontainers.image.description="API y procesos en segundo plano de un operador de FlowRide" \
      org.opencontainers.image.source="https://github.com/jhonnyebc/FlowRide" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${REVISION}"
WORKDIR /app
COPY --from=compilacion /publicado/api .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FlowRide.API.dll"]

FROM ${ASPNET} AS borde
ARG VERSION=0.0.0-local
ARG REVISION=local
LABEL org.opencontainers.image.title="flowride-borde" \
      org.opencontainers.image.description="Borde común de FlowRide: gateway, ruteo por operador y router de webhooks" \
      org.opencontainers.image.source="https://github.com/jhonnyebc/FlowRide" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${REVISION}"
WORKDIR /app
COPY --from=compilacion /publicado/borde .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FlowRide.Borde.dll"]

FROM ${RUNTIME} AS aprovisionamiento
ARG VERSION=0.0.0-local
ARG REVISION=local
LABEL org.opencontainers.image.title="flowride-aprovisionamiento" \
      org.opencontainers.image.description="Herramienta de plataforma: alta y migración de operadores, con los migration bundles" \
      org.opencontainers.image.source="https://github.com/jhonnyebc/FlowRide" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${REVISION}"
WORKDIR /app
COPY --from=compilacion /publicado/aprovisionamiento .
COPY --from=migraciones /publicado/migraciones ./migraciones
USER $APP_UID
ENTRYPOINT ["dotnet", "flowride-aprovisionamiento.dll"]
