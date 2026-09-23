#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds, packages, and deploys MuseumAdmin to Azure App Service.
.PARAMETER ResourceGroup
    Azure resource group name.
.PARAMETER AppName
    Azure Web App name.
#>
param(
    [string]$ResourceGroup = "tution",
    [string]$AppName = "MuseumAdmin1",
    [string]$Configuration = "Release",
    [string]$PublishDir = "publish",
    [string]$ZipFile = "app.zip"
)

$ErrorActionPreference = "Stop"

$RepoRoot = $PSScriptRoot
Set-Location $RepoRoot

Write-Host "Publishing project ($Configuration)..." -ForegroundColor Cyan
dotnet publish -c $Configuration -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$ZipPath = Join-Path $RepoRoot $ZipFile
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Write-Host "Creating deployment package $ZipFile..." -ForegroundColor Cyan
Push-Location (Join-Path $RepoRoot $PublishDir)
try {
    Compress-Archive -Path * -DestinationPath $ZipPath -Force
}
finally {
    Pop-Location
}

Write-Host "Deploying to Azure Web App '$AppName' in resource group '$ResourceGroup'..." -ForegroundColor Cyan
az webapp deploy --resource-group $ResourceGroup --name $AppName --src-path $ZipPath --type zip
if ($LASTEXITCODE -ne 0) { throw "az webapp deploy failed" }

Write-Host "Deployment complete." -ForegroundColor Green
