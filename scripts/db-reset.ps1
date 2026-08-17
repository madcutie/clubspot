<#
.SYNOPSIS
  Borra la base de desarrollo y la deja limpia para que la API la vuelva a crear.

.DESCRIPTION
  Tira el volumen de PostgreSQL y levanta el contenedor de nuevo, vacío.
  NO migra ni siembra: eso lo hace la API al arrancar en Development
  (dev-up.ps1, o dotnet run). Así nunca hay dos instancias de la API peleando el puerto.

  La base de desarrollo es descartable: este script borra todos sus datos.

.EXAMPLE
  .\scripts\db-reset.ps1
  .\scripts\dev-up.ps1
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $repo 'compose.yaml'

if (-not $Force -and -not $PSCmdlet.ShouldProcess('la base de desarrollo clubspot', 'BORRAR todos los datos')) {
    return
}

$api = Get-Process 'ClubSpot.Api' -ErrorAction SilentlyContinue
if ($api) {
    Write-Host 'Deteniendo la API que está usando la base...' -ForegroundColor Yellow
    $api | Stop-Process -Force
    Start-Sleep -Seconds 2
}

Write-Host 'Borrando el volumen de PostgreSQL...' -ForegroundColor Yellow
docker compose -f $compose down -v | Out-Null

Write-Host 'Levantando PostgreSQL vacío...' -ForegroundColor Yellow
docker compose -f $compose up -d postgres | Out-Null

Write-Host ''
Write-Host 'Base vacía. Arrancá la API para que migre y siembre:' -ForegroundColor Green
Write-Host '  .\scripts\dev-up.ps1' -ForegroundColor Green
