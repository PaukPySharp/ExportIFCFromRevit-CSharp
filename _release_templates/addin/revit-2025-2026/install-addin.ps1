param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

function Wait-BeforeClose {
    param(
        [int]$TimeoutSeconds = 300
    )

    Write-Host ""
    Write-Host "Нажмите любую клавишу, чтобы закрыть окно сразу,"
    Write-Host "или подождите $TimeoutSeconds сек."

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if ([Console]::KeyAvailable) {
            [void][Console]::ReadKey($true)
            return
        }

        Start-Sleep -Milliseconds 200
    }
}

try {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

    $addinName = "ExportIFCFromRevit-CSharp"
    $payloadDir = Join-Path $scriptDir $addinName
    $dllName = "ExportIfc.RevitAddin.Net8.dll"
    $sourceDllPath = Join-Path $payloadDir $dllName
    $supportedYears = @("2025", "2026")

    if (-not (Test-Path -LiteralPath $payloadDir)) {
        throw "Add-in payload folder not found: '$payloadDir'."
    }

    if (-not (Test-Path -LiteralPath $sourceDllPath)) {
        throw "Main add-in assembly not found: '$sourceDllPath'."
    }

    foreach ($year in $supportedYears) {
        $addinRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$year"
        $addinBinDir = Join-Path $addinRoot $addinName
        $addinFile = Join-Path $addinRoot "$addinName.addin"

        New-Item -ItemType Directory -Force -Path $addinRoot | Out-Null
        New-Item -ItemType Directory -Force -Path $addinBinDir | Out-Null

        Get-ChildItem -Path $addinBinDir -Force -ErrorAction SilentlyContinue |
            Remove-Item -Force -Recurse

        Copy-Item -Path (Join-Path $payloadDir "*") -Destination $addinBinDir -Recurse -Force

        $targetDll = Join-Path $addinBinDir $dllName

        $xml = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>$addinName</Name>
    <Assembly>$targetDll</Assembly>
    <AddInId>8D83F7A9-6F3A-4E7E-9D27-0C9CE2E8F7F1</AddInId>
    <FullClassName>ExportIfc.RevitAddin.App</FullClassName>
    <VendorId>PaukPySharp</VendorId>
    <VendorDescription>Export IFC by PaukPySharp</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

        Set-Content -Path $addinFile -Value $xml -Encoding UTF8

        Write-Host "Installed add-in for Revit $year"
        Write-Host "Manifest: $addinFile"
        Write-Host "Folder:   $addinBinDir"
        Write-Host ""
    }

    Write-Host "Done."
}
catch {
    Write-Error $_
}
finally {
    Wait-BeforeClose -TimeoutSeconds 300
}