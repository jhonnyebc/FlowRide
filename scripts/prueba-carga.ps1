<#
.SYNOPSIS
Prueba de carga con k6 (QA-03, QA-07): hasta 200 usuarios concurrentes por el camino WAF → borde → API → PostgreSQL.

.DESCRIPTION
k6 corre en un contenedor dentro de la red de docker compose y entra por el WAF. Necesita arriba los contenedores, el
borde y la API del operador. El borde limita a 300 peticiones por minuto por IP y en la prueba todo llega desde la IP
del WAF: para medir capacidad hay que arrancar el borde con el límite subido solo durante la prueba:
    $env:Borde__PeticionesPorMinutoPanel = '1000000'; dotnet run --project src/FlowRide.Borde --launch-profile https
Sin eso, la prueba muestra el límite de tasa actuando (respuestas 429) y no pasa sus umbrales.
El resumen queda en scripts/carga/resultado.json (no se versiona).

.EXAMPLE
./scripts/prueba-carga.ps1

.EXAMPLE
./scripts/prueba-carga.ps1 -Perfil humo
#>
param(
    [ValidateSet('carga', 'humo')] [string] $Perfil = 'carga',
    [int] $Usuarios = 200,
    [string] $Operador = 'tenant-a'
)

$ErrorActionPreference = 'Stop'
$carpeta = Join-Path $PSScriptRoot 'carga'

docker run --rm --network flowride_default `
    -e PERFIL=$Perfil -e USUARIOS=$Usuarios -e FLOWRIDE_HOST="$Operador.dev.localhost" `
    -v "${carpeta}:/carga" `
    grafana/k6:2.2.0 run --summary-export /carga/resultado.json /carga/carga.js
$codigo = $LASTEXITCODE

if ($codigo -eq 0) { Write-Host "`nRESULTADO: se cumplieron los umbrales (P95 < 500 ms, P99 < 1,5 s, errores < 1 %)." }
else { Write-Host "`nRESULTADO: no se cumplieron todos los umbrales (código $codigo). Revisa el resumen de arriba." }
exit $codigo
