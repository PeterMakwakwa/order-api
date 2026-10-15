<#
.SYNOPSIS
    Issues a local JWT for the Orders API with dotnet user-jwts and prints it.

.DESCRIPTION
    The signing key lives in your dotnet user-secrets (created on first use), never in the repository.
    appsettings.Localhost.json already trusts the dotnet-user-jwts issuer for http://localhost:5088 (IDE) and
    http://localhost:5000 (container), so the token works for both.

    -UpdateEnvFile also copies the signing key into src/.env (ORDER_JWT_SIGNING_KEY), so the API container started with
    "docker compose --profile api up" accepts the same tokens.

.EXAMPLE
    ./build/local/New-LocalToken.ps1                    # customer 42
    ./build/local/New-LocalToken.ps1 -CustomerId 7
    ./build/local/New-LocalToken.ps1 -Support           # support staff: reads any customer's orders, cannot place orders
    ./build/local/New-LocalToken.ps1 -UpdateEnvFile     # and share the signing key with the API container
#>
[CmdletBinding()]
param(
    [ValidateRange(1, [int]::MaxValue)]
    [int] $CustomerId = 42,

    [switch] $Support,

    [string] $ValidFor = '8h',

    [switch] $UpdateEnvFile
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$apiProject = Join-Path $repositoryRoot 'src\Order.Presentation.Api\Order.Presentation.Api.csproj'
$envFile = Join-Path $repositoryRoot 'src\.env'

# user-jwts writes the issuer and audiences into an appsettings file. appsettings.Localhost.json already has them,
# so give it a throwaway file instead of letting it rewrite the committed one.
$scratchSettingsFile = Join-Path ([System.IO.Path]::GetTempPath()) ("order-user-jwts-{0}.json" -f [guid]::NewGuid())
Set-Content -Path $scratchSettingsFile -Value '{}' -Encoding utf8

$arguments = @(
    'user-jwts', 'create',
    '--project', $apiProject,
    '--appsettings-file', $scratchSettingsFile,
    '--audience', 'http://localhost:5088',
    '--audience', 'http://localhost:5000',
    '--valid-for', $ValidFor,
    '--output', 'token'
)

if ($Support) {
    $arguments += @('--name', 'support-user', '--role', 'Support')
}
else {
    $arguments += @('--name', "customer-$CustomerId", '--claim', "customer_id=$CustomerId")
}

try {
    $output = @(& dotnet @arguments)
    $exitCode = $LASTEXITCODE
}
finally {
    Remove-Item $scratchSettingsFile -ErrorAction SilentlyContinue
}

$token = ($output | Select-Object -Last 1)
if ($exitCode -ne 0 -or [string]::IsNullOrWhiteSpace($token)) {
    throw "dotnet user-jwts failed to issue a token: $($output -join ' ')"
}
$token = $token.Trim()

if ($UpdateEnvFile) {
    if (-not (Test-Path $envFile)) {
        throw "src/.env not found. Copy src/.env.example to src/.env first."
    }

    $secrets = & dotnet user-secrets list --project $apiProject
    $signingKey = ($secrets | Where-Object { $_ -match '^Authentication:Schemes:Bearer:SigningKeys:0:Value = (.+)$' } | ForEach-Object { $Matches[1] }) | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($signingKey)) {
        throw 'No dotnet-user-jwts signing key found in user-secrets.'
    }

    $lines = @(Get-Content $envFile | Where-Object { $_ -notmatch '^\s*ORDER_JWT_SIGNING_KEY\s*=' })
    $lines += "ORDER_JWT_SIGNING_KEY=$signingKey"
    Set-Content -Path $envFile -Value $lines -Encoding utf8
    Write-Host 'src/.env updated with ORDER_JWT_SIGNING_KEY (recreate the api container to pick it up).' -ForegroundColor Green
}

Write-Host "Authorization: Bearer <token> for $(if ($Support) { 'support staff' } else { "customer $CustomerId" }), valid for $ValidFor`:" -ForegroundColor Green
Write-Output $token
