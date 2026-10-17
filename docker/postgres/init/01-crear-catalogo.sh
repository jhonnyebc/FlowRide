#!/usr/bin/env bash
# Crea la base del catálogo de plataforma con su propio rol (ADR-0002, ADR-0014).
# Los Silos de los operadores NO se crean aquí: los aprovisiona la herramienta
# src/FlowRide.Aprovisionamiento (RF-03, ADR-0010).
# Solo se ejecuta cuando el volumen de datos está vacío (primer arranque).
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
     -v clave="$CATALOGO_DB_PASSWORD" <<'SQL'
CREATE ROLE flowride_catalogo LOGIN PASSWORD :'clave';
CREATE DATABASE flowride_catalogo OWNER flowride_catalogo;
-- Ningún otro rol puede conectarse al catálogo.
REVOKE ALL ON DATABASE flowride_catalogo FROM PUBLIC;
GRANT CONNECT, TEMPORARY ON DATABASE flowride_catalogo TO flowride_catalogo;
SQL
