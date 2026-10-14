#!/usr/bin/env bash
# Crea la base de catálogo y una base por tenant, cada una con un rol propio (ADR-0002, ADR-0014).
# Solo se ejecuta cuando el volumen de datos está vacío (primer arranque).
set -euo pipefail

crear_silo() {
  local base="$1" rol="$2" clave="$3"
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
       -v base="$base" -v rol="$rol" -v clave="$clave" <<'SQL'
CREATE ROLE :"rol" LOGIN PASSWORD :'clave';
CREATE DATABASE :"base" OWNER :"rol";
-- Ningún otro rol puede conectarse a este Silo.
REVOKE ALL ON DATABASE :"base" FROM PUBLIC;
GRANT CONNECT, TEMPORARY ON DATABASE :"base" TO :"rol";
SQL
}

crear_silo flowride_catalogo flowride_catalogo "$CATALOGO_DB_PASSWORD"
crear_silo flowride_tenant_a flowride_tenant_a "$TENANT_A_DB_PASSWORD"
crear_silo flowride_tenant_b flowride_tenant_b "$TENANT_B_DB_PASSWORD"
