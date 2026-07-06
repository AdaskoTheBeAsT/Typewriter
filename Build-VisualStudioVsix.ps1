[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [string] $Framework = "net472",

    [string] $OutputDirectory,

    [switch] $NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$projectRoot = Join-Path $repoRoot "src/Typewriter.VisualStudio"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts"
}

$projectPath = Join-Path $projectRoot "Typewriter.VisualStudio.csproj"
$manifestPath = Join-Path $projectRoot "source.extension.vsixmanifest"

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    Write-Host "> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
    }
}

[xml] $manifest = Get-Content -LiteralPath $manifestPath
$identity = $manifest.SelectSingleNode("/*[local-name()='PackageManifest']/*[local-name()='Metadata']/*[local-name()='Identity']")
if ($null -eq $identity) {
    throw "Unable to find VSIX identity in $manifestPath."
}

$version = $identity.GetAttribute("Version")
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Unable to read VSIX version from $manifestPath."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$buildArguments = @(
    "build",
    $projectPath,
    "--configuration", $Configuration,
    "-m:1"
)

if ($NoRestore) {
    $buildArguments += "--no-restore"
}

Invoke-ExternalCommand "dotnet" $buildArguments

$sourceVsix = Join-Path $projectRoot "bin/$Configuration/$Framework/Typewriter.VisualStudio.vsix"
if (-not (Test-Path -LiteralPath $sourceVsix)) {
    throw "Visual Studio VSIX was not produced: $sourceVsix"
}

$outputVsix = Join-Path $OutputDirectory "Typewriter.VisualStudio-$version.vsix"
Copy-Item -LiteralPath $sourceVsix -Destination $outputVsix -Force

if (-not (Test-Path -LiteralPath $outputVsix)) {
    throw "Visual Studio VSIX was not copied to: $outputVsix"
}

if ((Get-Item -LiteralPath $outputVsix).Length -le 0) {
    throw "Visual Studio VSIX is empty: $outputVsix"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::OpenRead($outputVsix)
try {
    $entries = @($zip.Entries | ForEach-Object { $_.FullName.Replace("\", "/") })

    foreach ($requiredEntry in @(
        "extension.vsixmanifest",
        "tools/typewriter-cli/Typewriter.Cli.dll",
        "tools/typewriter-cli/Typewriter.Cli.deps.json",
        "tools/typewriter-cli/Typewriter.Cli.runtimeconfig.json",
        "tools/typewriter-cli/Buildalyzer.Logger.dll",
        "tools/typewriter-cli/Buildalyzer.Logger/net472/Buildalyzer.Logger.dll",
        "tools/typewriter-lsp/Typewriter.LanguageServer.dll",
        "tools/typewriter-lsp/Typewriter.LanguageServer.deps.json",
        "tools/typewriter-lsp/Typewriter.LanguageServer.runtimeconfig.json",
        "tools/typewriter-lsp/Typewriter.Engine.xml",
        "tools/typewriter-lsp/Typewriter.Abstractions.xml",
        "tools/typewriter-lsp/Buildalyzer.Logger.dll",
        "tools/typewriter-lsp/Buildalyzer.Logger/net472/Buildalyzer.Logger.dll"
    )) {
        if ($requiredEntry -notin $entries) {
            throw "Visual Studio VSIX is missing expected entry: $requiredEntry"
        }
    }

    $toolExecutables = @($entries | Where-Object {
        $_.StartsWith("tools/", [System.StringComparison]::Ordinal) -and
        $_.EndsWith(".exe", [System.StringComparison]::OrdinalIgnoreCase)
    })

    if ($toolExecutables.Count -gt 0) {
        throw "Visual Studio VSIX should package DLL-based tools only. Unexpected EXE entries: $($toolExecutables -join ', ')"
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Packaged Visual Studio VSIX:"
Write-Host $outputVsix
