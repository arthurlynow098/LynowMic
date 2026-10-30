$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Instale o SDK .NET 8 em https://dotnet.microsoft.com/download/dotnet/8.0 e abra novamente o PowerShell.'
}
dotnet publish "$root/windows/LynowMic.Windows/LynowMic.Windows.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o "$root/releases/windows"
if ($LASTEXITCODE -ne 0) { throw 'A compilação falhou. Veja o erro acima.' }
Write-Host "Pronto: $root/releases/windows/LynowMic.exe"
