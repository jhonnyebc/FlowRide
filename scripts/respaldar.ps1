<#
.SYNOPSIS
Respalda el Silo de uno o de todos los operadores activos en su propio bucket (ADR-0013).

.DESCRIPTION
pg_dump (formato custom, comprimido) corre dentro del contenedor flowride-postgres con el superusuario del servidor,
que el contenedor ya conoce: el script no maneja contraseñas. La herramienta de aprovisionamiento guarda el archivo en
el bucket del operador (respaldos/silo-<fecha>.dump) y borra los de más de 35 días, salvo el más reciente.
Se puede programar con el Programador de tareas de Windows (en la nube lo hace la plataforma, ADR-0010).

.EXAMPLE
./scripts/respaldar.ps1 -Operador tenant-a

.EXAMPLE
./scripts/respaldar.ps1    # todos los operadores activos
#>
param([string[]] $Operador)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun.ps1')

$operadores = if ($Operador) { $Operador } else { Get-OperadoresActivos }
if (-not $operadores) { throw 'No hay operadores activos en el catálogo.' }

$temporal = New-Item -ItemType Directory -Force (Join-Path ([IO.Path]::GetTempPath()) "flowride-respaldos-$PID")
try {
    foreach ($id in $operadores) {
        $silo = Get-NombreSilo $id
        $archivo = "$silo.dump"
        $local = Join-Path $temporal $archivo
        $reloj = [Diagnostics.Stopwatch]::StartNew()

        Write-Host "== $id ($silo)"
        Invoke-EnPostgres "pg_dump -U `$POSTGRES_USER -Fc -d $silo -f /tmp/$archivo"
        docker cp "flowride-postgres:/tmp/$archivo" $local | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "No se pudo copiar el respaldo de $silo desde el contenedor." }
        Invoke-EnPostgres "rm -f /tmp/$archivo"

        Invoke-Herramienta respaldar --id $id --archivo $local
        Write-Host ("   Tiempo total: {0:N1} s" -f $reloj.Elapsed.TotalSeconds)
    }
}
finally {
    Remove-Item -Recurse -Force $temporal -ErrorAction SilentlyContinue
}
