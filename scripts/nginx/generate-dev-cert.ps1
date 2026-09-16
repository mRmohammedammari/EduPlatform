<#
.SYNOPSIS
    Generates a self-signed TLS certificate for local HTTPS testing of the Nginx reverse proxy.

.DESCRIPTION
    For real public deployment, replace nginx/certs/eduplatform.crt and eduplatform.key
    with a certificate issued by a trusted CA (e.g. Let's Encrypt via certbot), as described
    in docs/PRODUCTION_READINESS.md. This script is only meant for local/dev HTTPS testing.
#>

$certDir = Join-Path $PSScriptRoot "..\..\nginx\certs"
New-Item -ItemType Directory -Force -Path $certDir | Out-Null

$crtPath = Join-Path $certDir "eduplatform.crt"
$keyPath = Join-Path $certDir "eduplatform.key"

if ((Test-Path $crtPath) -and (Test-Path $keyPath)) {
    Write-Host "Certificate already exists at $certDir. Delete the files to regenerate." -ForegroundColor Yellow
    exit 0
}

$opensslCmd = Get-Command openssl -ErrorAction SilentlyContinue
if (-not $opensslCmd) {
    Write-Error "OpenSSL is required to generate the local dev certificate but was not found on PATH."
    exit 1
}

# Run via a disposable Docker container to avoid local OpenSSL config/path quirks on Windows.
docker run --rm -v "${certDir}:/certs" alpine/openssl req -x509 -nodes -newkey rsa:2048 -days 730 `
    -keyout /certs/eduplatform.key `
    -out /certs/eduplatform.crt `
    -subj "/CN=localhost" `
    -addext "subjectAltName=DNS:localhost,DNS:eduplatform.local"

if ($LASTEXITCODE -ne 0) {
    Write-Error "OpenSSL (via Docker) failed to generate the certificate."
    exit 1
}

Write-Host "Self-signed certificate generated at $certDir (valid for localhost, dev only)." -ForegroundColor Green
