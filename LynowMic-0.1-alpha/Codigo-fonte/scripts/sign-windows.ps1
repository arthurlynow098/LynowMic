param(
    [Parameter(Mandatory = $true)]
    [string]$Executable,

    [Parameter(Mandatory = $true)]
    [string]$CertificatePath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
    throw "Executável não encontrado: $Executable"
}

if (-not (Test-Path -LiteralPath $CertificatePath -PathType Leaf)) {
    throw "Certificado não encontrado: $CertificatePath"
}

if (-not $env:LYNOW_SIGNING_PASSWORD) {
    throw 'Defina LYNOW_SIGNING_PASSWORD no ambiente antes de assinar.'
}

$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $signtool) {
    throw 'signtool.exe não encontrado. Instale o Windows SDK.'
}

& $signtool.FullName sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /f $CertificatePath /p $env:LYNOW_SIGNING_PASSWORD $Executable
if ($LASTEXITCODE -ne 0) { throw 'A assinatura Authenticode falhou.' }

& $signtool.FullName verify /pa /v $Executable
if ($LASTEXITCODE -ne 0) { throw 'A assinatura foi aplicada, mas a validação falhou.' }

Write-Host "Assinatura válida: $Executable"
