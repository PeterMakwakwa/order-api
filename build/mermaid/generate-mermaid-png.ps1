<#
.SYNOPSIS
    Renders every docs/**/*.mermaid file to a .png next to it, so the images referenced in the docs stay
    in sync with the diagram sources. Runs after Debug builds of Order.Presentation.Api.

.NOTES
    Requires the Mermaid CLI:  npm install -g @mermaid-js/mermaid-cli
    Without it the script prints a message and exits successfully, so builds never fail because of docs.
    Only diagrams whose .mermaid file is newer than its .png are rendered. Use -Force to render all.
#>
[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$docsDirectory = Resolve-Path (Join-Path $PSScriptRoot '..\..\docs')

if (-not (Get-Command mmdc -ErrorAction SilentlyContinue)) {
    # Informational only: MSBuild would turn a PowerShell warning into a build warning.
    Write-Output 'Skipping diagram generation: Mermaid CLI (mmdc) is not installed (npm install -g @mermaid-js/mermaid-cli).'
    exit 0
}

$mermaidFiles = Get-ChildItem -Path $docsDirectory -Recurse -Filter *.mermaid

foreach ($file in $mermaidFiles) {
    $outputFile = Join-Path $file.DirectoryName "$($file.BaseName).png"

    if (-not $Force -and (Test-Path $outputFile) -and ((Get-Item $outputFile).LastWriteTimeUtc -ge $file.LastWriteTimeUtc)) {
        continue
    }

    mmdc --input $file.FullName --output $outputFile --backgroundColor white --scale 2 --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "Mermaid CLI failed for $($file.FullName)."
    }

    Write-Output "Converted $($file.FullName) to $outputFile"
}
