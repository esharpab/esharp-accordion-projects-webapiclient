<#
.SYNOPSIS
    Downloads swagger.json from a running AccordionQ2 WebApi instance.
    Use this when the API changes to review what needs updating in WebApiClient.

.DESCRIPTION
    The WebApiClient is hand-written and mirrors the server API.
    When endpoints or models change, run this script to fetch the latest
    swagger.json, then manually update the relevant files:

        WebApiClient/Models/     - DTOs, enums, request models
        WebApiClient/Groups/     - endpoint methods

.PARAMETER ServerUrl
    Base URL of the running WebApi. Defaults to http://localhost:5000.

.EXAMPLE
    .\regenerate.ps1
    .\regenerate.ps1 -ServerUrl "http://raspberrypi:5000"
#>
param(
    [string]$ServerUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"
$outputPath = Join-Path $PSScriptRoot "swagger.json"
$swaggerUrl = "$($ServerUrl.TrimEnd('/'))/swagger/v1/swagger.json"

Write-Host "Downloading swagger.json from $swaggerUrl ..." -ForegroundColor Cyan

try {
    Invoke-WebRequest $swaggerUrl -OutFile $outputPath
    Write-Host "Saved to: $outputPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Compare the new swagger.json with the existing client code." -ForegroundColor Yellow
    Write-Host "  2. Update WebApiClient/Models/ for any new/changed DTOs or enums." -ForegroundColor Yellow
    Write-Host "  3. Update WebApiClient/Groups/ for any new/changed endpoints." -ForegroundColor Yellow
    Write-Host "  4. Update WebApiClient/AccordionQ2Client.cs if new groups are needed." -ForegroundColor Yellow
}
catch {
    Write-Error "Failed to download swagger.json. Is the WebApi running at $ServerUrl ?"
}
