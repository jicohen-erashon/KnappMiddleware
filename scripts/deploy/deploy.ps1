<#
.SYNOPSIS
  Despliega KnappMiddleware (Postgres + RabbitMQ + migrate + admin-seed + Api) con Docker Compose.

.DESCRIPTION
  Se ejecuta en el servidor del ambiente (srv-bidev), desde el runner de GitHub, con Windows
  PowerShell 5.1. Los secretos y variables llegan como variables de entorno (las define el workflow
  desde el Environment de GitHub); el script nunca los imprime ni los recibe por linea de comandos.

  A diferencia del patron de cohen-insight-api, no copia el compose a una carpeta de destino
  separada: corre directo desde el checkout (Cohen-KnappMiddleware/docker-compose.yml). Esto es
  seguro porque db/migrations vive en el MISMO repo/commit (no hay nada externo que sincronizar) y
  porque .env se reescribe entero en cada deploy desde los secretos — nada del checkout necesita
  sobrevivir entre corridas, y los datos persisten en volumenes nombrados de Docker, no en el path
  del checkout.

  Flujo (si algo falla antes del paso 5, la Api que ya estaba corriendo no se toca):
    1. Valida que esten todas las variables obligatorias y que ninguna tenga caracteres que
       rompan el .env.
    2. Descarga la imagen del registry con una configuracion de Docker temporal.
    3. Escribe Cohen-KnappMiddleware/.env (no se versiona).
    4. "docker compose up" (sin --build: ya se usa la imagen descargada) — Postgres/RabbitMQ
       saludables -> migrate (falla y se detiene si alguna migracion falla) -> admin-seed -> Api.
    5. Espera a que /health responda; si no, falla mostrando los logs de la Api.

.PARAMETER Environment
  dev, qa o prd. Define el nombre del proyecto de Compose y el sufijo de los nombres de contenedor
  (knapp-postgres-dev, etc.) para poder convivir con otra infraestructura ya existente en el mismo
  servidor sin chocar en nombres ni puertos.

.PARAMETER ImageRef
  Imagen completa a desplegar, p. ej. srv-bidev:5000/knapp-middleware:sha-0123456789ab.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][ValidateSet('dev', 'qa', 'prd')][string]$Environment,
  [Parameter(Mandatory = $true)][string]$ImageRef,
  [int]$HealthTimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$composeDir = Join-Path $repoRoot 'Cohen-KnappMiddleware'
$composeFile = Join-Path $composeDir 'docker-compose.yml'
$project = "knapp-middleware-$Environment"

. (Join-Path $PSScriptRoot 'lib.ps1')

function Invoke-Compose([string[]]$ComposeArgs) {
  $base = @('compose', '--project-name', $project, '--project-directory', $composeDir, '-f', $composeFile, '--env-file', (Join-Path $composeDir '.env'))
  return (Invoke-Docker ($base + $ComposeArgs))
}

function Get-Setting([string]$Name) {
  $value = [Environment]::GetEnvironmentVariable($Name)
  if ([string]::IsNullOrWhiteSpace($value)) { return $null }
  return $value.Trim()
}

# ───────────────── 1. Variables obligatorias y opcionales ─────────────────

Write-Step "Validando la configuracion del ambiente '$Environment'"

$required = @('POSTGRES_PASSWORD', 'RABBITMQ_PASSWORD', 'REGISTRY_USER', 'REGISTRY_PASSWORD')
$optional = @(
  'POSTGRES_DB', 'POSTGRES_USER', 'RABBITMQ_USER',
  'ADMIN_USERNAME', 'ADMIN_PASSWORD', 'ADMIN_ROLE',
  'MIDDLEWARE_PORT', 'POSTGRES_PORT', 'RABBITMQ_PORT', 'RABBITMQ_MANAGEMENT_PORT',
  'LOKI_ENDPOINT'
)

$settings = @{}
$missing = @()
foreach ($name in $required) {
  $value = Get-Setting $name
  if ($null -eq $value) { $missing += $name } else { $settings[$name] = $value }
}
if ($missing.Count -gt 0) {
  throw "Faltan variables obligatorias en el Environment '$Environment': $($missing -join ', ')"
}
foreach ($name in $optional) {
  $value = Get-Setting $name
  if ($null -ne $value) { $settings[$name] = $value }
}

$unsafe = @($settings.Keys | Where-Object { $settings[$_] -match "['\r\n]" })
if ($unsafe.Count -gt 0) {
  throw "Estos valores contienen comillas simples o saltos de linea y no se pueden escribir en .env: $($unsafe -join ', '). Genera un valor sin esos caracteres."
}

$registryHost = Get-RegistryHost $ImageRef
Write-Host "Configuracion valida. Imagen: $ImageRef"

# ───────────────── 2. Imagen ─────────────────

Write-Step "Descargando $ImageRef"
$dockerConfig = New-DockerAuthConfig $registryHost $settings['REGISTRY_USER'] $settings['REGISTRY_PASSWORD']
try {
  if ((Invoke-Docker @('--config', $dockerConfig, 'pull', $ImageRef)) -ne 0) {
    throw "No se pudo descargar $ImageRef. Revisa que la imagen exista en $registryHost y que REGISTRY_USER / REGISTRY_PASSWORD sean correctos."
  }
}
finally {
  Remove-DockerAuthConfig $dockerConfig
}

# ───────────────── 3. .env ─────────────────

Write-Step 'Escribiendo Cohen-KnappMiddleware/.env'

# Sufijo por ambiente en nombres de contenedor y puertos publicados: este mismo servidor ya corre
# otra infraestructura (ver README) con los nombres/puertos default de Postgres y RabbitMQ — sin
# esto, un deploy automatizado chocaria con lo que ya esta corriendo.
$envValues = [ordered]@{
  MIDDLEWARE_IMAGE          = $ImageRef
  ASPNETCORE_ENVIRONMENT    = 'Production'  # nunca Development en un servidor real: ver README (expone /scalar y /openapi sin auth).
  MIGRATIONS_PATH           = '../db/migrations'  # mismo repo/commit que el codigo: ver docstring arriba.
  POSTGRES_DB               = $settings['POSTGRES_DB']
  POSTGRES_USER             = $settings['POSTGRES_USER']
  POSTGRES_PASSWORD         = $settings['POSTGRES_PASSWORD']
  POSTGRES_PORT             = $settings['POSTGRES_PORT']
  POSTGRES_CONTAINER_NAME   = "knapp-postgres-$Environment"
  RABBITMQ_USER             = $settings['RABBITMQ_USER']
  RABBITMQ_PASSWORD         = $settings['RABBITMQ_PASSWORD']
  RABBITMQ_PORT             = $settings['RABBITMQ_PORT']
  RABBITMQ_MANAGEMENT_PORT  = $settings['RABBITMQ_MANAGEMENT_PORT']
  RABBITMQ_CONTAINER_NAME   = "knapp-rabbitmq-$Environment"
  MIGRATE_CONTAINER_NAME    = "knapp-migrate-$Environment"
  ADMIN_SEED_CONTAINER_NAME = "knapp-admin-seed-$Environment"
  MIDDLEWARE_CONTAINER_NAME = "knapp-middleware-$Environment"
  MIDDLEWARE_PORT           = $settings['MIDDLEWARE_PORT']
  LOKI_ENDPOINT             = $settings['LOKI_ENDPOINT']
  ADMIN_USERNAME            = $settings['ADMIN_USERNAME']
  ADMIN_PASSWORD            = $settings['ADMIN_PASSWORD']
  ADMIN_ROLE                = $settings['ADMIN_ROLE']
}
# Las claves opcionales sin valor se omiten en vez de escribirse vacias, para que el default del
# propio docker-compose.yml (${VAR:-default}) aplique igual que en desarrollo local.
$envPath = Join-Path $composeDir '.env'
$lines = foreach ($key in $envValues.Keys) {
  $value = $envValues[$key]
  if ($null -ne $value -and $value -ne '') { "$key='$value'" }
}
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($envPath, (($lines -join "`n") + "`n"), $utf8NoBom)
Write-Host "Escritas $($lines.Count) variables."

# ───────────────── 4. Stack completo ─────────────────
# postgres/rabbitmq (healthy) -> migrate (si falla, "up" se detiene aqui: la Api anterior sigue
# corriendo, sin tocar) -> admin-seed -> middleware. Misma cadena probada en desarrollo local.

Write-Step 'Levantando el stack'
if ((Invoke-Compose @('up', '-d', '--remove-orphans')) -ne 0) {
  throw 'docker compose no pudo levantar el stack completo (ver el paso que fallo arriba — probablemente migrate o admin-seed).'
}

# ───────────────── 5. Healthcheck de la Api ─────────────────

$hostPort = if ($settings.ContainsKey('MIDDLEWARE_PORT')) { $settings['MIDDLEWARE_PORT'] } else { '5231' }
$container = "knapp-middleware-$Environment"
Write-Step "Esperando a que la Api responda /health (maximo $HealthTimeoutSeconds s)"
$deadline = (Get-Date).AddSeconds($HealthTimeoutSeconds)
$healthy = $false
while ((Get-Date) -lt $deadline) {
  try {
    $response = Invoke-WebRequest -Uri "http://localhost:$hostPort/health" -UseBasicParsing -TimeoutSec 5
    if ($response.StatusCode -eq 200) { $healthy = $true; break }
  }
  catch { }
  Start-Sleep -Seconds 5
}
if (-not $healthy) {
  Write-Host "La Api no respondio. Ultimas lineas del log de ${container}:"
  Invoke-Docker @('logs', '--tail', '80', $container) | Out-Null
  throw 'La Api no quedo saludable.'
}

Write-Host ''
Write-Host '==============================================='
Write-Host "  Despliegue completado: $Environment"
Write-Host "  Imagen:  $ImageRef"
Write-Host "  Api:     http://$([System.Net.Dns]::GetHostName()):$hostPort/health"
Write-Host '==============================================='
