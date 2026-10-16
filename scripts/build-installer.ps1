[CmdletBinding()]
param([string]$Iscc = $env:VELQORA_ISCC, [switch]$Sign)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$props = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props')
$version = [string]$props.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Versão inválida em Directory.Build.props.' }
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne "v$version") { throw 'A tag não corresponde à versão centralizada.' }
if (-not $Iscc) { $Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path -LiteralPath $Iscc)) { throw 'Instale Inno Setup 6.7.3 ou informe -Iscc com o caminho do ISCC.exe.' }
$project = Join-Path $root 'MacroDoButler.csproj'
dotnet build $project --configuration Release -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar Velqora em Release.' }
$dist = Join-Path $root 'dist'
$compiled = Join-Path $root 'bin\Release\net48'
$payload = Join-Path $dist ('payload-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $payload | Out-Null
foreach ($name in @('Velqora.exe', 'Velqora.exe.config')) {
    if (-not (Test-Path -LiteralPath (Join-Path $compiled $name))) { throw "Arquivo compilado ausente: $name" }
    Copy-Item -LiteralPath (Join-Path $compiled $name) -Destination (Join-Path $payload $name)
}
# Explicit allowlist: never package the repository or a user's installation directory.
if ($Sign) { & (Join-Path $PSScriptRoot 'Sign-Artifact.ps1') -Path (Join-Path $payload 'Velqora.exe') }
$release = Join-Path $dist ('release-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $release | Out-Null
& $Iscc "/DAppVersion=$version" "/DPayload=$payload" "/DOutput=$release" (Join-Path $root 'installer\Velqora.iss')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o instalador.' }
$installer = Join-Path $release "Velqora-Setup-v$version.exe"
if (-not (Test-Path -LiteralPath $installer)) { throw 'O instalador não foi criado.' }
if ($Sign) { & (Join-Path $PSScriptRoot 'Sign-Artifact.ps1') -Path $installer }
try { Copy-Item -LiteralPath (Join-Path $payload 'Velqora.exe') -Destination (Join-Path $dist 'Velqora.exe') -Force }
catch { Write-Warning 'dist\Velqora.exe está em uso; o executável novo está na pasta release gerada abaixo.' }
try { Copy-Item -LiteralPath (Join-Path $payload 'Velqora.exe.config') -Destination (Join-Path $dist 'Velqora.exe.config') -Force }
catch { Write-Warning 'dist\Velqora.exe.config está em uso; a configuração nova está na pasta release.' }
Copy-Item -LiteralPath (Join-Path $payload 'Velqora.exe') -Destination (Join-Path $release 'Velqora.exe')
Copy-Item -LiteralPath (Join-Path $payload 'Velqora.exe') -Destination (Join-Path $release "Velqora-v$version.exe")
Copy-Item -LiteralPath (Join-Path $payload 'Velqora.exe.config') -Destination (Join-Path $release 'Velqora.exe.config')
Copy-Item -LiteralPath (Join-Path $payload 'Velqora.exe.config') -Destination (Join-Path $release "Velqora-v$version.exe.config")
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $release 'LICENSE.txt')
$files = Get-ChildItem -LiteralPath $release -File | Sort-Object Name
$hashes = foreach ($file in $files) { '{0}  {1}' -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $file.Name }
$hashes | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ascii
(Get-FileHash -LiteralPath (Join-Path $release 'Velqora.exe')).Hash.ToLowerInvariant() | Set-Content -LiteralPath (Join-Path $release 'Velqora.sha256') -Encoding ascii
$status = if ($Sign) { 'ASSINADO: executável e instalador verificados por SignTool /pa /all /tw e Authenticode.' } else { 'NÃO ASSINADO: nenhum certificado configurado. Não existe identidade de editor verificada.' }
"Velqora $version`r`n$status" | Set-Content -LiteralPath (Join-Path $release 'SIGNING-STATUS.txt') -Encoding utf8
"Velqora $version`r`n`r`n$status`r`n`r`nInstale usando Velqora-Setup-v$version.exe. Dados pessoais são preservados em %LOCALAPPDATA%\Velqora. Consulte README.md para migração e recuperação." | Set-Content -LiteralPath (Join-Path $release 'RELEASE-NOTES.md') -Encoding utf8
if ($env:GITHUB_OUTPUT) { "release_dir=$release" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8 }
Write-Host "Distribuição: $release"
Write-Host $status
