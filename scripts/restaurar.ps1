<#
.SYNOPSIS
Restaura un respaldo del Silo de un operador desde su bucket (ADR-0013).

.DESCRIPTION
Por defecto restaura en una base aparte (flowride_<operador>_restaurada) para revisarla sin tocar la del operador.
Con -Reemplazar restaura sobre la base real del operador: antes de hacerlo hay que detener la API de ese operador.
Los objetos quedan a nombre del rol del Silo, como los crea la herramienta de aprovisionamiento.

.EXAMPLE
./scripts/restaurar.ps1 -Operador tenant-a

.EXAMPLE
./scripts/restaurar.ps1 -Operador tenant-a -Nombre respaldos/silo-20261031T120000Z.dump -Reemplazar
#>
param(
    [Parameter(Mandatory)] [string] $Operador,
    [string] $Nombre,
    [string] $BaseDestino,
    [switch] $Reemplazar
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

$silo = Get-NombreSilo $Operador
if ($Reemplazar) {
    if ($BaseDestino -and $BaseDestino -ne $silo) { throw '-Reemplazar restaura sobre la base del operador; no uses -BaseDestino con él.' }
    $BaseDestino = $silo
    $confirmacion = Read-Host "Se reemplazarán los datos de $silo con el respaldo. Detén antes la API de $Operador. Escribe '$Operador' para continuar"
    if ($confirmacion -ne $Operador) { throw 'Cancelado.' }
}
elseif (-not $BaseDestino) {
    $BaseDestino = "${silo}_restaurada"
}
if ($BaseDestino -notmatch '^[a-z0-9_]{1,63}$') { throw "Nombre de base no válido: '$BaseDestino'." }

$temporal = New-Item -ItemType Directory -Force (Join-Path ([IO.Path]::GetTempPath()) "flowride-restaurar-$PID")
try {
    $local = Join-Path $temporal "$silo.dump"
    $argumentos = @('descargar-respaldo', '--id', $Operador, '--archivo', $local)
    if ($Nombre) { $argumentos += @('--nombre', $Nombre) }
    Invoke-Herramienta @argumentos

    $reloj = [Diagnostics.Stopwatch]::StartNew()
    docker cp $local "flowride-postgres:/tmp/restaurar-$silo.dump" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo copiar el respaldo al contenedor.' }

    if ($Reemplazar) {
        # Mismo dueño y mismos permisos de la base: se reemplaza su contenido, no la base.
        Invoke-Sql 'postgres' "select pg_terminate_backend(pid) from pg_stat_activity where datname = '$silo' and pid <> pg_backend_pid()" | Out-Null
        Invoke-EnPostgres "pg_restore -U `$POSTGRES_USER --clean --if-exists --no-owner --role=$silo --exit-on-error -d $silo /tmp/restaurar-$silo.dump"
    }
    else {
        Invoke-EnPostgres "dropdb -U `$POSTGRES_USER --if-exists $BaseDestino"
        Invoke-EnPostgres "createdb -U `$POSTGRES_USER $BaseDestino"
        Invoke-EnPostgres "pg_restore -U `$POSTGRES_USER --no-owner --exit-on-error -d $BaseDestino /tmp/restaurar-$silo.dump"
    }
    Invoke-EnPostgres "rm -f /tmp/restaurar-$silo.dump"
    Write-Host ("Restaurado en {0} en {1:N1} s." -f $BaseDestino, $reloj.Elapsed.TotalSeconds)
}
finally {
    Remove-Item -Recurse -Force $temporal -ErrorAction SilentlyContinue
}
