[CmdletBinding()]
param([Parameter(Mandatory)][string]$Path, [string]$Thumbprint = $env:VELQORA_SIGN_THUMBPRINT,
      [string]$SignTool = $env:VELQORA_SIGNTOOL,
      [string]$TimestampUrl = 'http://timestamp.digicert.com')
$ErrorActionPreference = 'Stop'
if (-not $Thumbprint) { throw 'Certificado não configurado. A assinatura foi solicitada sem thumbprint.' }
if (-not $SignTool) {
    $SignTool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $SignTool -or -not (Test-Path -LiteralPath $SignTool)) { throw 'SignTool do Windows SDK não encontrado.' }
$certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$Thumbprint"
if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date)) { throw 'Certificado sem chave privada ou expirado.' }
if ('1.3.6.1.5.5.7.3.3' -notin @($certificate.EnhancedKeyUsageList.ObjectId.Value)) { throw 'O certificado não permite code signing.' }
# The password/private key is never passed to a command-line process.
& $SignTool sign /s My /sha1 $Thumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $Path
if ($LASTEXITCODE -ne 0) { throw "Falha ao assinar $Path" }
& $SignTool verify /pa /all /tw $Path
if ($LASTEXITCODE -ne 0) { throw "Assinatura ou timestamp inválido: $Path" }
if ((Get-AuthenticodeSignature -LiteralPath $Path).Status -ne 'Valid') { throw "Authenticode inválido: $Path" }
