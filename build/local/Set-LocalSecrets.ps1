<#
.SYNOPSIS
    Copies the API's database credentials from src/.env into dotnet user-secrets, so the API can be run
    from the IDE (Localhost environment) against the docker compose SQL Server.

.EXAMPLE
    ./build/local/Set-LocalSecrets.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$envFile = Join-Path $repositoryRoot 'src\.env'
$apiProject = Join-Path $repositoryRoot 'src\Order.Presentation.Api\Order.Presentation.Api.csproj'

if (-not (Test-Path $envFile)) {
    throw "src/.env not found. Copy src/.env.example to src/.env and fill in the passwords first."
}

$settings = @{}
foreach ($line in Get-Content $envFile) {
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$') {
        $settings[$Matches[1]] = $Matches[2]
    }
}

foreach ($required in 'ORDER_DB_USER', 'ORDER_DB_PASSWORD') {
    if ([string]::IsNullOrWhiteSpace($settings[$required])) {
        throw "$required is empty in src/.env."
    }
}

dotnet user-secrets set 'ApplicationConfiguration:Persistence:ApplicationDatabase:Username' $settings['ORDER_DB_USER'] --project $apiProject | Out-Null
dotnet user-secrets set 'ApplicationConfiguration:Persistence:ApplicationDatabase:Password' $settings['ORDER_DB_PASSWORD'] --project $apiProject | Out-Null

Write-Host 'User-secrets updated for Order.Presentation.Api (database username and password).'
