<#
.SYNOPSIS
Prueba de recuperación del Silo de un operador (ADR-0010: RPO ≤ 15 min, RTO ≤ 2 h; ADR-0013: respaldos en su bucket).

.DESCRIPTION
1. Cuenta las filas de cada tabla del Silo.
2. Toma un respaldo y lo guarda en el bucket del operador (respaldar.ps1).
3. Lo descarga y lo restaura en una base aparte (restaurar.ps1); la base del operador no se toca.
4. Compara tabla por tabla y mide los tiempos; al final borra la base de prueba.
Si el operador está recibiendo cambios mientras corre, pueden aparecer diferencias pequeñas: son filas posteriores
al respaldo, no pérdidas.

.EXAMPLE
./scripts/prueba-recuperacion.ps1 -Operador tenant-a
#>
param([Parameter(Mandatory)] [string] $Operador)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

$silo = Get-NombreSilo $Operador
$prueba = "${silo}_prueba_recuperacion"

Write-Host "Prueba de recuperación de $Operador ($silo)`n"
$antes = Get-FilasPorTabla $silo

$total = [Diagnostics.Stopwatch]::StartNew()
& (Join-Path $PSScriptRoot 'respaldar.ps1') -Operador $Operador
$tiempoRespaldo = $total.Elapsed

$restauracion = [Diagnostics.Stopwatch]::StartNew()
& (Join-Path $PSScriptRoot 'restaurar.ps1') -Operador $Operador -BaseDestino $prueba
$tiempoRestauracion = $restauracion.Elapsed
$total.Stop()

try {
    $despues = Get-FilasPorTabla $prueba
    $diferencias = @($antes.Keys | Where-Object { $despues[$_] -ne $antes[$_] })
    $faltantes = @($antes.Keys | Where-Object { -not $despues.Contains($_) })

    Write-Host "`nTabla                                         Original   Restaurada"
    foreach ($tabla in $antes.Keys) {
        $marca = if ($despues[$tabla] -eq $antes[$tabla]) { 'OK' } else { 'DIFERENTE' }
        Write-Host ("{0,-45} {1,9} {2,12}  {3}" -f $tabla, $antes[$tabla], $despues[$tabla], $marca)
    }

    $filas = ($antes.Values | Measure-Object -Sum).Sum
    Write-Host ''
    Write-Host ("Tablas: {0}, filas: {1}. Tablas con diferencias: {2}. Tablas faltantes: {3}." -f $antes.Count, $filas, $diferencias.Count, $faltantes.Count)
    Write-Host ("Respaldo (pg_dump + bucket): {0:N1} s" -f $tiempoRespaldo.TotalSeconds)
    Write-Host ("Restauración (bucket + pg_restore): {0:N1} s" -f $tiempoRestauracion.TotalSeconds)
    Write-Host ("Tiempo de recuperación medido: {0:N1} s (objetivo RTO de ADR-0010: 2 h)" -f $total.Elapsed.TotalSeconds)
    Write-Host 'Pérdida máxima de datos (RPO) en local: el intervalo entre respaldos programados. El objetivo de 15 min'
    Write-Host 'de ADR-0010 lo da en la nube el respaldo continuo (WAL/PITR) del servidor de PostgreSQL administrado.'

    if ($diferencias.Count -gt 0 -or $faltantes.Count -gt 0) { exit 1 }
    Write-Host "`nRESULTADO: la restauración coincide con la base original."
}
finally {
    Invoke-EnPostgres "dropdb -U `$POSTGRES_USER --if-exists $prueba"
}
