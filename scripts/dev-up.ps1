<#
.SYNOPSIS
  Levanta el entorno de desarrollo completo: PostgreSQL, la API y los dos frontends.

.DESCRIPTION
  Cada servicio arranca en su propia ventana de PowerShell, así queda vivo y se puede
  ver su salida. Para frenar todo, cerrá las ventanas (o usá scripts/dev-down.ps1).

  La API migra y siembra la base sola al arrancar en Development.

  ⚠️ ESTE SCRIPT ES PARA EL USUARIO, NO PARA LOS AGENTES. Abre ventanas sueltas que un
  agente no puede manejar ni frenar. Un agente levanta y baja en background sólo el
  servicio que necesita — ver "Cómo levanta un agente lo que necesita" en AGENTS.md.

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
Start-Service-Window 'ClubSpot JobService' (Join-Path $repo 'src\backend') `
    "`$env:DOTNET_ENVIRONMENT='Development'; dotnet run --project src/Jobs/ClubSpot.JobService"
Start-Service-Window 'ClubSpot Backoffice' (Join-Path $repo 'src\frontend\backoffice') 'npm run dev'
Start-Service-Window 'ClubSpot Reservas' (Join-Path $repo 'src\frontend\reservas') 'npm run dev'

# Sin el tunel, Mercado Pago falla en silencio: el comprador paga, la plata se cobra y la reserva
# se queda en pendingPayment porque ni el webhook ni el rebote de vuelta llegan a la API.
$tunnelUrl = 'noe-uncephalic-jerome.ngrok-free.dev'
if (Get-Process -Name ngrok -ErrorAction SilentlyContinue) {
    Write-Host '  ngrok ya estaba corriendo' -ForegroundColor DarkGray
} elseif (Get-Command ngrok -ErrorAction SilentlyContinue) {
    Start-Service-Window 'ClubSpot ngrok' $repo "ngrok http 5037 --url=$tunnelUrl"
} else {
    Write-Host '  ngrok NO esta instalado: Mercado Pago no va a funcionar' -ForegroundColor Red
}

Write-Host ''
Write-Host 'Portal de reservas   http://localhost:5183' -ForegroundColor Green
Write-Host 'Backoffice           http://localhost:5184' -ForegroundColor Green
Write-Host 'API                  http://localhost:5037' -ForegroundColor Green
Write-Host 'JobService           (sin puerto; concilia pagos cada 5 min)' -ForegroundColor Green
Write-Host 'PostgreSQL           localhost:5432' -ForegroundColor Green
Write-Host "Tunel Mercado Pago   https://$tunnelUrl  (webhook y vuelta del pago)" -ForegroundColor Green
Write-Host ''
Write-Host 'La API tarda unos segundos en migrar y sembrar la primera vez.' -ForegroundColor DarkGray
