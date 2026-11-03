# Funciones compartidas por los scripts de operación (respaldos y recuperación). Se cargan con: . ./comun.ps1
# Todo lo que toca PostgreSQL corre dentro del contenedor flowride-postgres con su superusuario ($POSTGRES_USER del
# contenedor): los scripts no leen ni piden contraseñas.

$script:RaizRepositorio = Split-Path $PSScriptRoot -Parent
$script:ProyectoHerramienta = Join-Path $script:RaizRepositorio 'src/FlowRide.Aprovisionamiento'

function Invoke-EnPostgres([string] $Comando) {
    docker exec flowride-postgres sh -c $Comando
    if ($LASTEXITCODE -ne 0) { throw "Falló en el contenedor de PostgreSQL: $Comando" }
}

# Ejecuta SQL en una base y devuelve las filas (columnas separadas por '|').
function Invoke-Sql([string] $Base, [string] $Sql) {
    $filas = $Sql | docker exec -i flowride-postgres sh -c "psql -U `$POSTGRES_USER -d $Base -tA -v ON_ERROR_STOP=1"
    if ($LASTEXITCODE -ne 0) { throw "Falló la consulta en $Base." }
    $filas | Where-Object { $_ }
}

function Get-OperadoresActivos {
    Invoke-Sql 'flowride_catalogo' "select id from plataforma.operador where estado = 'Activo' order by id"
}

# Misma regla que Operador.NombreSiloDe: flowride_ + id con guiones bajos.
function Get-NombreSilo([string] $Id) {
    if ($Id -notmatch '^[a-z0-9][a-z0-9-]{0,39}$') { throw "Id de operador no válido: '$Id'." }
    'flowride_' + $Id.Replace('-', '_')
}

function Invoke-Herramienta {
    dotnet run --project $script:ProyectoHerramienta --no-launch-profile -- @args
    if ($LASTEXITCODE -ne 0) { throw "La herramienta de aprovisionamiento falló: $($args -join ' ')" }
}

# Filas de cada tabla de los esquemas del Silo, para comparar una base con su restauración.
function Get-FilasPorTabla([string] $Base) {
    $sql = @"
select table_schema || '.' || table_name || '|' ||
       (xpath('/row/c/text()', query_to_xml(format('select count(*) as c from %I.%I', table_schema, table_name), false, true, '')))[1]::text
from information_schema.tables
where table_type = 'BASE TABLE' and table_schema not in ('pg_catalog', 'information_schema')
order by 1
"@
    $resultado = [ordered]@{}
    foreach ($fila in Invoke-Sql $Base $sql) {
        $tabla, $cantidad = $fila -split '\|'
        $resultado[$tabla] = [long]$cantidad
    }
    $resultado
}
