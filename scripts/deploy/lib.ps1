<#
.SYNOPSIS
  Utilidades compartidas por publish.ps1 y deploy.ps1. Dot-source: ". (Join-Path $PSScriptRoot 'lib.ps1')".
#>

function Write-Step([string]$Message) {
  Write-Host ''
  Write-Host "==> $Message" -ForegroundColor Cyan
}

# "srv-bidev:5000/knapp-middleware:sha-abc123" -> "srv-bidev:5000"
function Get-RegistryHost([string]$ImageRef) {
  $slash = $ImageRef.IndexOf('/')
  if ($slash -lt 0) { throw "ImageRef '$ImageRef' no trae host de registry (falta la '/')." }
  return $ImageRef.Substring(0, $slash)
}

function Invoke-Docker([string[]]$DockerArgs) {
  & docker @DockerArgs
  return $LASTEXITCODE
}

function Get-DockerOutput([string[]]$DockerArgs) {
  $output = & docker @DockerArgs 2>$null
  return ($output -join "`n").Trim()
}

# Config de Docker temporal con las credenciales del registry, para no tocar ~/.docker/config.json
# del usuario del runner ni dejar la password en el historial de comandos. Se usa con "docker
# --config <dir> ..." y se borra siempre al terminar (ver Remove-DockerAuthConfig).
function New-DockerAuthConfig([string]$RegistryHost, [string]$User, [string]$Password) {
  $dir = Join-Path ([System.IO.Path]::GetTempPath()) ("docker-auth-" + [System.Guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Path $dir | Out-Null
  $auth = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("${User}:${Password}"))
  $config = @{ auths = @{ $RegistryHost = @{ auth = $auth } } } | ConvertTo-Json -Depth 4
  $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::WriteAllText((Join-Path $dir 'config.json'), $config, $utf8NoBom)
  return $dir
}

function Remove-DockerAuthConfig([string]$Dir) {
  if ($Dir -and (Test-Path $Dir)) { Remove-Item -Recurse -Force $Dir }
}
