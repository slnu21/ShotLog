#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Runs the Windows App Certification Kit (WACK) against the locally built ShotLog MSIX.

.DESCRIPTION
  WACK (appcert.exe) needs (1) Administrator rights and (2) an *installable* (signed, trusted)
  package, because it deploys the app to test it. The Store upload package is intentionally
  UNSIGNED (the Store signs it), so this script signs the **test** .msixbundle with a throwaway
  self-signed certificate whose subject equals the package Publisher, trusts it, runs WACK, then
  removes the temporary trust + cert. The distribution *.msixupload is never touched.

  Run from an ELEVATED PowerShell at the repo root:
      powershell -ExecutionPolicy Bypass -File scripts\run-wack.ps1

  Output: build\wack\wack-<package>.xml  (open in a browser; OVERALL_RESULT = PASS/FAIL)
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$publisher = 'CN=1398342C-A2D7-4B4A-BFE2-34D8CCFD7FBA'   # must match Package.appxmanifest <Identity Publisher>

# --- locate inputs/tools ---
$bundle = Get-ChildItem -Recurse 'src\ShotLog.Package\AppPackages' -Filter *.msixbundle -ErrorAction SilentlyContinue |
          Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $bundle) { throw "No .msixbundle under src\ShotLog.Package\AppPackages. Build it first (StoreUpload MSBuild)." }

$signtool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if (-not $signtool) { throw 'signtool.exe (Windows SDK, x64) not found.' }
$appcert = 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe'
if (-not (Test-Path $appcert)) { throw "appcert.exe not found ($appcert). Install the Windows App Certification Kit." }

$work   = Join-Path $root 'build\wack'
New-Item -ItemType Directory -Force $work | Out-Null
$pfx    = Join-Path $work 'shotlog-test.pfx'
$cer    = Join-Path $work 'shotlog-test.cer'
$report = Join-Path $work ("wack-{0}.xml" -f $bundle.BaseName)
$pwText = 'shotlog'

Write-Host "Package : $($bundle.FullName)"
Write-Host "Report  : $report`n"

# --- 1) throwaway self-signed cert (subject == Publisher) ---
$cert = New-SelfSignedCertificate -Type Custom -Subject $publisher -KeyUsage DigitalSignature `
  -FriendlyName 'ShotLog WACK test (safe to delete)' -CertStoreLocation 'Cert:\CurrentUser\My' `
  -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
$pw = ConvertTo-SecureString $pwText -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $pw | Out-Null
Export-Certificate    -Cert $cert -FilePath $cer | Out-Null

try {
    # --- 2) sign the TEST bundle (NOT the Store .msixupload) ---
    & $signtool sign /fd SHA256 /f $pfx /p $pwText $bundle.FullName
    if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)." }

    # --- 3) trust the cert so the package can deploy ---
    Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null

    # --- 4) run WACK ---
    & $appcert reset
    & $appcert test -appxpackagepath $bundle.FullName -reportoutputpath $report
    Write-Host "`nWACK report written: $report" -ForegroundColor Green
}
finally {
    # --- 5) clean up temp trust + cert (leave the report + pfx in build\wack, which is git-ignored) ---
    Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force -ErrorAction SilentlyContinue
    Get-ChildItem 'Cert:\LocalMachine\TrustedPeople' -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq $publisher } | Remove-Item -Force -ErrorAction SilentlyContinue
}
