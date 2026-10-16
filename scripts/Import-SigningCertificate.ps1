$ErrorActionPreference = 'Stop'
if (-not $env:VELQORA_PFX_BASE64 -or -not $env:VELQORA_PFX_PASSWORD) { throw 'Configure os dois secrets de assinatura.' }
# Import in-process: no password arguments, no temporary PFX on disk, no key export.
$bytes = [Convert]::FromBase64String($env:VELQORA_PFX_BASE64)
$password = ConvertTo-SecureString $env:VELQORA_PFX_PASSWORD -AsPlainText -Force
$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $bytes, $password, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet)
[Array]::Clear($bytes, 0, $bytes.Length)
$store = [System.Security.Cryptography.X509Certificates.X509Store]::new('My', 'CurrentUser')
try { $store.Open('ReadWrite'); $store.Add($certificate) } finally { $store.Close() }
"VELQORA_SIGN_THUMBPRINT=$($certificate.Thumbprint)" | Out-File -FilePath $env:GITHUB_ENV -Append -Encoding utf8
$password.Dispose()
$certificate.Dispose()
