<#
.SYNOPSIS
  Compila el backoffice y el portal de reservas para un entorno real.

.DESCRIPTION
  Deja dos carpetas dist/ listas para subir a un hosting estático. La dirección de la API se
  hornea en los dos bundles, así que este comando se corre una vez por entorno: el dist/ de test
  y el de producción no son el mismo archivo aunque el commit sea idéntico.

  El hosting no construye nada. Sube el dist/ que sale de acá.

.PARAMETER ApiUrl
  Dirección pública de la API, sin barra final. Por ejemplo https://api.miclub.com.ar

.PARAMETER SkipContractCheck
  Saltea el `dotnet build` que verifica que el contrato OpenAPI versionado esté al día.
  Sólo cuando ya se compiló el backend en esta misma sesión.

.EXAMPLE
  .\scripts\build-frontends.ps1 -ApiUrl https://api.miclub.com.ar
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [string]$ApiUrl,

  [switch]$SkipContractCheck
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

function Abort($message) {
  Write-Host "  x $message" -ForegroundColor Red
  exit 1
}

# Se valida antes de instalar nada: equivocarse en la URL cuesta un npm ci de más, no un deploy roto.
if ($ApiUrl.EndsWith('/')) { Abort "La URL no puede terminar en barra: '$ApiUrl'" }
if ($ApiUrl -notmatch '^https://') {
  Abort "La URL tiene que ser https. Si estás probando contra tu máquina, este script no es el camino: usá npm run build con VITE_ALLOW_LOCAL_API=1"
}
try { $parsed = [Uri]$ApiUrl } catch { Abort "No es una URL válida: '$ApiUrl'" }
if ($parsed.IsLoopback) { Abort "La URL apunta a esta máquina: '$ApiUrl'" }

Write-Host ''
Write-Host "API   $ApiUrl" -ForegroundColor Cyan

# El cliente que se hornea sale del contrato versionado. Si el contrato quedó viejo, los dos
# frontends salen hablando un idioma que la API desplegada no entiende, y el build no puede notarlo
# solo: `npm run build` no compila el backend.
if (-not $SkipContractCheck) {
  Write-Host ''
  Write-Host 'Contrato  compilando la Api para reescribir el documento OpenAPI...' -ForegroundColor Cyan
  dotnet build (Join-Path $repo 'src/backend') --nologo --verbosity quiet
  if ($LASTEXITCODE -ne 0) { Abort 'El backend no compila. Los frontends se compilan contra su contrato, así que se frena acá.' }

  $contract = 'docs/api/clubspot.openapi.json'
  # `diff --name-only` y no `status`: el documento se reescribe con LF y el árbol de trabajo lo
  # guarda con CRLF, así que `status` lo marca modificado aunque el contenido sea idéntico.
  $drift = git -C $repo diff --name-only -- $contract
  if ($drift) {
    Abort "El contrato versionado estaba desactualizado: $contract cambió al compilar. Revisá el diff y commiteálo antes de compilar los frontends."
  }
  Write-Host '          al día' -ForegroundColor DarkGray
}

$env:VITE_API_URL = $ApiUrl
Remove-Item Env:\VITE_ALLOW_LOCAL_API -ErrorAction SilentlyContinue

$apps = @(
  @{ Name = 'Backoffice'; Path = 'src/frontend/backoffice' },
  @{ Name = 'Reservas';   Path = 'src/frontend/reservas'   }
)

foreach ($app in $apps) {
  $dir = Join-Path $repo $app.Path
  Write-Host ''
  Write-Host "$($app.Name)  $($app.Path)" -ForegroundColor Cyan

  Push-Location $dir
  try {
    # `npm ci` y no `npm i`: orval está declarado con rango abierto y un cliente regenerado
    # distinto del versionado se subiría sin que nadie lo note.
    npm ci --silent
    if ($LASTEXITCODE -ne 0) { Abort "npm ci falló en $($app.Path)" }

    npm run build
    if ($LASTEXITCODE -ne 0) { Abort "El build falló en $($app.Path)" }
  }
  finally { Pop-Location }

  # El guard de vite.config.ts mira lo que entra; esto mira lo que sale, que es lo que se sube.
  $dist = Join-Path $dir 'dist'
  $bundles = Get-ChildItem -Path (Join-Path $dist 'assets') -Filter *.js -ErrorAction SilentlyContinue

  if (-not ($bundles | Select-String -Pattern ([regex]::Escape($ApiUrl)) -List)) {
    Remove-Item -Recurse -Force $dist
    Abort "El bundle de $($app.Name) no tiene adentro la URL que se le pidió. Se borró el dist/ para que no se suba."
  }

  # Se busca loopback **con puerto**, que es la forma que tiene una dirección de API. Un
  # `http://localhost` pelado no se cuenta: react-router lo usa internamente como base ficticia.
  $leak = $bundles | Select-String -Pattern '(localhost|127\.0\.0\.1|\[::1\]):\d+' -List
  if ($leak) {
    Remove-Item -Recurse -Force $dist
    Abort "El bundle de $($app.Name) quedó apuntando a esta máquina. Se borró el dist/ para que no se suba."
  }

  Write-Host "          dist listo: $dist" -ForegroundColor Green
}

Write-Host ''
Write-Host 'Listo. Subí cada dist/ a su sitio estático.' -ForegroundColor Green
Write-Host 'El hosting tiene que reescribir cualquier ruta a /index.html (vercel.json y public/_redirects ya van adentro).' -ForegroundColor DarkGray
Write-Host ''
Write-Host 'Y en la API de ese entorno, los orígenes de los dos sitios tienen que estar en:' -ForegroundColor DarkGray
Write-Host '  Cors__AllowedOrigins__0 / __1      y      Payments__AllowedReturnOrigins__0 / __1' -ForegroundColor DarkGray
Write-Host ''
