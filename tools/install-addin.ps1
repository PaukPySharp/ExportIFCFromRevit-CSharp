param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("2022", "2023", "2024", "2025", "2026")]
    [string]$Year,

    [Parameter(Mandatory = $false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [Parameter(Mandatory = $false)]
    [string]$AddinName = "ExportIFCFromRevit-CSharp"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
    $repoRoot = Split-Path -Parent $scriptDir

    switch ($Year) {
        { $_ -in @("2022", "2023", "2024") } {
            $projectName = "ExportIfc.RevitAddin.Net48"
            $dllName = "ExportIfc.RevitAddin.Net48.dll"
            break
        }

        { $_ -in @("2025", "2026") } {
            $projectName = "ExportIfc.RevitAddin.Net8"
            $dllName = "ExportIfc.RevitAddin.Net8.dll"
            break
        }

        default {
            throw "Unknown Revit year: $Year"
        }
    }

    # Add-in projects are expected to be built for x64.
    # AppendTargetFrameworkToOutputPath=false, so the DLL is located directly in bin\x64\<Configuration>.
    $buildOutputDir = Join-Path $repoRoot "src\$projectName\bin\x64\$Configuration"
    $sourceDllPath = Join-Path $buildOutputDir $dllName

    if (-not (Test-Path -LiteralPath $sourceDllPath)) {
        throw "Build output not found: '$sourceDllPath'. Build project '$projectName' first using configuration '$Configuration | x64'."
    }

    # Install into the per-user Revit Addins folder.
    $addinRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$Year"
    $addinBinDir = Join-Path $addinRoot $AddinName
    $addinFile = Join-Path $addinRoot "$AddinName.addin"

    New-Item -ItemType Directory -Force -Path $addinRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $addinBinDir | Out-Null

    # Clear the destination folder to avoid mixing files from different add-in branches.
    Get-ChildItem -Path $addinBinDir -Force -ErrorAction SilentlyContinue |
        Remove-Item -Force -Recurse

    # Copy the full build output, not just the main DLL.
    Copy-Item -Path (Join-Path $buildOutputDir "*") -Destination $addinBinDir -Recurse -Force

    $targetDll = Join-Path $addinBinDir $dllName

    $xml = @"
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>$AddinName</Name>
    <Assembly>$targetDll</Assembly>
    <AddInId>8D83F7A9-6F3A-4E7E-9D27-0C9CE2E8F7F1</AddInId>
    <FullClassName>ExportIfc.RevitAddin.App</FullClassName>
    <VendorId>PaukPySharp</VendorId>
    <VendorDescription>Export IFC by PaukPySharp</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

    Set-Content -Path $addinFile -Value $xml -Encoding UTF8

    Write-Host "Installed add-in for Revit $Year"
    Write-Host "Project:  $projectName"
    Write-Host "Source:   $sourceDllPath"
    Write-Host "Manifest: $addinFile"
    Write-Host "Folder:   $addinBinDir"
}
catch {
    Write-Error $_
}
finally {
    Wait-BeforeClose -TimeoutSeconds 300
}
