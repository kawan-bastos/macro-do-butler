$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$directory = Join-Path $root '.tools'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$download = Join-Path $directory 'innosetup-6.7.3.exe'
Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $download
if ((Get-FileHash -LiteralPath $download).Hash -ne '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732') { throw 'SHA-256 do compilador inesperado.' }
$signature = Get-AuthenticodeSignature -LiteralPath $download
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*O=Pyrsys B.V.*') { throw 'Assinatura do compilador inválida.' }
$target = Join-Path $directory 'Inno Setup 6'
$process = Start-Process -FilePath $download -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS','/TASKS=', ('/DIR="' + $target + '"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Instalação do compilador falhou: $($process.ExitCode)" }
if ($env:GITHUB_ENV) { "VELQORA_ISCC=$target\ISCC.exe" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8 }
Write-Host "Compilador: $target\ISCC.exe"
