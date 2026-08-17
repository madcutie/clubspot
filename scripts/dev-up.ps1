<#
.SYNOPSIS
  Levanta el entorno de desarrollo completo: PostgreSQL, la API y los dos frontends.

.DESCRIPTION
  Cada servicio arranca en su propia ventana de PowerShell, así queda vivo y se puede
  ver su salida. Para frenar todo, cerrá las ventanas (o usá scripts/dev-down.ps1).

  La API migra y siembra la base sola al arrancar en Development.

.EXAMPLE
  .\scripts\dev-up.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

function Start-Service-Window {
    param([string]$Title, [string]$WorkingDirectory, [string]$Command)
    $script = "`$Host.UI.RawUI.WindowTitle = '$Title'; Set-Location '$WorkingDirectory'; $Command"
    Start-Process pwsh -ArgumentList '-NoExit', '-Command', $script | Out-Null
    Write-Host "  $Title" -ForegroundColor DarkGray
}

Write-Host 'ClubSpot — entorno de desarrollo' -ForegroundColor Cyan

if (-not (Get-Process 'Docker Desktop' -ErrorAction SilentlyContinue)) {
    Write-Host 'Arrancando Docker Desktop...' -ForegroundColor Yellow
    Start-Process 'C:\Program Files\Docker\Docker\Docker Desktop.exe'
}

Write-Host 'Esperando el daemon de Docker...' -ForegroundColor Yellow
$deadline = (Get-Date).AddMinutes(3)
while ($true) {
    docker info 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { break }
    if ((Get-Date) -gt $deadline) { throw 'El daemon de Docker no respondió en 3 minutos.' }
    Start-Sleep -Seconds 3
}

Write-Host 'Levantando PostgreSQL...' -ForegroundColor Yellow
docker compose -f (Join-Path $repo 'compose.yaml') up -d postgres | Out-Null

Write-Host 'Abriendo servicios:' -ForegroundColor Yellow
Start-Service-Window 'ClubSpot API' (Join-Path $repo 'src\backend') `
    "`$env:ASPNETCORE_ENVIRONMENT='Development'; `$env:ASPNETCORE_URLS='http://localhost:5037'; dotnet run --project src/Api/ClubSpot.Api --no-launch-profile"
Start-Service-Window 'ClubSpot Backoffice' (Join-Path $repo 'src\frontend\backoffice') 'npm run dev'
Start-Service-Window 'ClubSpot Reservas' (Join-Path $repo 'src\frontend\reservas') 'npm run dev'

Write-Host ''
Write-Host 'Portal de reservas   http://localhost:5183' -ForegroundColor Green
Write-Host 'Backoffice           http://localhost:5184' -ForegroundColor Green
Write-Host 'API                  http://localhost:5037' -ForegroundColor Green
Write-Host 'PostgreSQL           localhost:5433' -ForegroundColor Green
Write-Host ''
Write-Host 'La API tarda unos segundos en migrar y sembrar la primera vez.' -ForegroundColor DarkGray
