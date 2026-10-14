$ErrorActionPreference = "Stop"

$projectDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectDirectory "MacroDoButler.csproj"
$destination = Join-Path $projectDirectory "dist"

dotnet build $projectFile --configuration Release

New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item `
    -LiteralPath (Join-Path $projectDirectory "bin\Release\net48\Macro do Butler.exe") `
    -Destination (Join-Path $destination "Macro do Butler.exe") `
    -Force

$configuration = Join-Path $projectDirectory "bin\Release\net48\Macro do Butler.exe.config"
if (Test-Path -LiteralPath $configuration) {
    Copy-Item `
        -LiteralPath $configuration `
        -Destination (Join-Path $destination "Macro do Butler.exe.config") `
        -Force
}

Write-Host "Executável criado em: $destination\Macro do Butler.exe"
