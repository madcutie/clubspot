<#
.SYNOPSIS
  Corre una consulta SQL contra la base de desarrollo.

.DESCRIPTION
  Envuelve psql dentro del contenedor (no hace falta tener psql instalado).
  Recordá que todos los identificadores son camelCase y van entre comillas dobles.

.EXAMPLE
  .\scripts\db-sql.ps1 'SELECT name FROM courts ORDER BY "sortOrder";'

.EXAMPLE
  .\scripts\db-sql.ps1 'SELECT * FROM "availabilityOverrides";'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [string]$Query
)

$ErrorActionPreference = 'Stop'
docker exec clubspot-postgres-1 psql -U postgres -d clubspot -c $Query
