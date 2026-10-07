<#
.SYNOPSIS
  Construye la imagen de KnappMiddleware y la publica en el registry privado.

.DESCRIPTION
  Se ejecuta en el runner de GitHub (self-hosted, srv-bidev), con Windows PowerShell 5.1. Construye
  el stage "final" de Cohen-KnappMiddleware/Dockerfile (contexto: KnappMiddleware/, el .csproj) con
  el tag indicado y lo sube. Las credenciales llegan por REGISTRY_USER/REGISTRY_PASSWORD (nunca por
  linea de comandos) y se usan con una configuracion de Docker temporal que se borra al terminar.

  El tag es inmutable (sha-<commit>): un tag nunca se reescribe, asi cada ambiente despliega
  exactamente lo que se probo y volver atras es redesplegar un tag anterior (workflow_dispatch con
  image_tag, ver deploy.yml).

.PARAMETER ImageRef
  Imagen completa, p. ej. srv-bidev:5000/knapp-middleware:sha-0123456789ab.

.PARAMETER Revision
  Commit que origino la imagen (se guarda como etiqueta OCI).

.PARAMETER Source
  URL del repositorio (se guarda como etiqueta OCI).
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$ImageRef,
  [string]$Revision = '',
  [string]$Source = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'lib.ps1')

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$buildContext = Join-Path $repoRoot 'KnappMiddleware'
$dockerfile = Join-Path $repoRoot 'Cohen-KnappMiddleware\Dockerfile'
$registryHost = Get-RegistryHost $ImageRef

$user = [Environment]::GetEnvironmentVariable('REGISTRY_USER')
$password = [Environment]::GetEnvironmentVariable('REGISTRY_PASSWORD')
if ([string]::IsNullOrWhiteSpace($user) -or [string]::IsNullOrWhiteSpace($password)) {
  throw 'Faltan REGISTRY_USER y/o REGISTRY_PASSWORD en el Environment.'
}
$user = $user.Trim()
$password = $password.Trim()

Write-Step "Construyendo $ImageRef"
$buildArgs = @('build', '--target', 'final', '-f', $dockerfile, '-t', $ImageRef)
if ($Revision) { $buildArgs += @('--label', "org.opencontainers.image.revision=$Revision") }
if ($Source) { $buildArgs += @('--label', "org.opencontainers.image.source=$Source") }
$buildArgs += $buildContext
if ((Invoke-Docker $buildArgs) -ne 0) { throw 'docker build fallo.' }

Write-Step "Publicando en $registryHost"
$dockerConfig = New-DockerAuthConfig $registryHost $user $password
try {
  if ((Invoke-Docker @('--config', $dockerConfig, 'push', $ImageRef)) -ne 0) {
    throw "No se pudo subir $ImageRef a $registryHost. Revisa REGISTRY_USER / REGISTRY_PASSWORD y que el registry este disponible."
  }
}
finally {
  Remove-DockerAuthConfig $dockerConfig
}

$digest = Get-DockerOutput @('inspect', '--format', '{{index .RepoDigests 0}}', $ImageRef)
Write-Host ''
Write-Host "Imagen publicada: $ImageRef"
if ($digest) { Write-Host "Digest: $digest" }
