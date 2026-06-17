[CmdletBinding()]
param(
    [string] $OutputDirectory,

    [switch] $InstallDependencies,

    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [string] $Framework = "net10.0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$vscodeRoot = Join-Path $repoRoot "vscode"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot $OutputDirectory
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

$npmCommand = if ($IsWindows) { "npm.cmd" } else { "npm" }
$packageJsonPath = Join-Path $vscodeRoot "package.json"
$packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
$version = [string] $packageJson.version
$toolRoot = Join-Path $OutputDirectory "typewriter-vscode-tools"
$cliPublishDir = Join-Path $toolRoot "typewriter-cli"
$languageServerPublishDir = Join-Path $toolRoot "typewriter-lsp"

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

function Publish-FrameworkDependentTool {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectPath,

        [Parameter(Mandatory = $true)]
        [string] $OutputPath,

        [Parameter(Mandatory = $true)]
        [string] $AssemblyName
    )

    $buildOutputPath = Join-Path $toolRoot "build-output/$AssemblyName/"
    foreach ($pathToReset in @($OutputPath, $buildOutputPath)) {
        if (Test-Path -LiteralPath $pathToReset) {
            Remove-Item -LiteralPath $pathToReset -Recurse -Force
        }
    }

    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

    Invoke-ExternalCommand "dotnet" @(
        "publish",
        $ProjectPath,
        "--configuration", $Configuration,
        "--framework", $Framework,
        "--self-contained", "false",
        "--output", $OutputPath,
        "-p:UseAppHost=false",
        "-p:SatelliteResourceLanguages=en",
        "-p:RunAnalyzers=false",
        "-p:BaseOutputPath=$buildOutputPath",
        "-m:1"
    )

    foreach ($requiredFile in @("$AssemblyName.dll", "$AssemblyName.deps.json", "$AssemblyName.runtimeconfig.json")) {
        $requiredPath = Join-Path $OutputPath $requiredFile
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Expected publish output is missing: $requiredPath"
        }
    }

    $nativeLaunchers = @(Get-ChildItem -LiteralPath $OutputPath -Recurse -File |
        Where-Object { $_.Extension -ieq ".exe" })

    if ($nativeLaunchers.Count -gt 0) {
        throw "Published VS Code tools must be DLL-based and cross-platform. Unexpected EXE files: $($nativeLaunchers.FullName -join ', ')"
    }
}

function Add-DirectoryToZip {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchive] $Zip,

        [Parameter(Mandatory = $true)]
        [string] $SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string] $EntryPrefix
    )

    $resolvedSourceDirectory = (Resolve-Path -LiteralPath $SourceDirectory).Path
    $files = Get-ChildItem -LiteralPath $resolvedSourceDirectory -Recurse -File |
        Where-Object { $_.Extension -notin @(".pdb", ".xml") }

    foreach ($file in $files) {
        $relativePath = [System.IO.Path]::GetRelativePath($resolvedSourceDirectory, $file.FullName).Replace("\", "/")
        $entryName = "$EntryPrefix/$relativePath"
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $Zip,
            $file.FullName,
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Unable to read VS Code extension version from $packageJsonPath."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$outputVsix = Join-Path $OutputDirectory "typewriter-vscode-$version.vsix"

Publish-FrameworkDependentTool `
    -ProjectPath (Join-Path $repoRoot "src/Typewriter.Cli/Typewriter.Cli.csproj") `
    -OutputPath $cliPublishDir `
    -AssemblyName "Typewriter.Cli"

Publish-FrameworkDependentTool `
    -ProjectPath (Join-Path $repoRoot "src/Typewriter.LanguageServer/Typewriter.LanguageServer.csproj") `
    -OutputPath $languageServerPublishDir `
    -AssemblyName "Typewriter.LanguageServer"

if ($InstallDependencies) {
    Invoke-ExternalCommand $npmCommand @("ci", "--prefix", $vscodeRoot)
}

Invoke-ExternalCommand $npmCommand @("--prefix", $vscodeRoot, "run", "lint")
Invoke-ExternalCommand $npmCommand @("--prefix", $vscodeRoot, "run", "bundle")

$bundlePath = Join-Path $vscodeRoot "dist/extension.js"
if (-not (Test-Path -LiteralPath $bundlePath)) {
    throw "Expected bundle output is missing: $bundlePath"
}

Invoke-ExternalCommand $npmCommand @(
    "--prefix", $vscodeRoot,
    "run", "package",
    "--",
    "--out", $outputVsix
)

if (-not (Test-Path -LiteralPath $outputVsix)) {
    throw "VS Code VSIX was not produced: $outputVsix"
}

if ((Get-Item -LiteralPath $outputVsix).Length -le 0) {
    throw "VS Code VSIX is empty: $outputVsix"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$packageArchive = [System.IO.Compression.ZipFile]::Open($outputVsix, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    $toolEntryPrefixes = @(
        "extension/tools/typewriter-cli/",
        "extension/tools/typewriter-lsp/"
    )

    $entriesToDelete = @($packageArchive.Entries | Where-Object {
        $entryName = $_.FullName
        $toolEntryPrefixes |
            Where-Object { $entryName.StartsWith($_, [System.StringComparison]::Ordinal) } |
            Select-Object -First 1
    })

    foreach ($entry in $entriesToDelete) {
        $entry.Delete()
    }

    Add-DirectoryToZip $packageArchive $cliPublishDir "extension/tools/typewriter-cli"
    Add-DirectoryToZip $packageArchive $languageServerPublishDir "extension/tools/typewriter-lsp"
}
finally {
    $packageArchive.Dispose()
}

$zip = [System.IO.Compression.ZipFile]::OpenRead($outputVsix)
try {
    $entries = @($zip.Entries | ForEach-Object { $_.FullName.Replace("\", "/") })

    foreach ($requiredEntry in @(
        "extension/package.json",
        "extension/dist/extension.js",
        "extension/tools/typewriter-cli/Typewriter.Cli.dll",
        "extension/tools/typewriter-cli/Typewriter.Cli.deps.json",
        "extension/tools/typewriter-cli/Typewriter.Cli.runtimeconfig.json",
        "extension/tools/typewriter-cli/Buildalyzer.Logger.dll",
        "extension/tools/typewriter-cli/Buildalyzer.Logger/net472/Buildalyzer.Logger.dll",
        "extension/tools/typewriter-lsp/Typewriter.LanguageServer.dll",
        "extension/tools/typewriter-lsp/Typewriter.LanguageServer.deps.json",
        "extension/tools/typewriter-lsp/Typewriter.LanguageServer.runtimeconfig.json",
        "extension/tools/typewriter-lsp/Buildalyzer.Logger.dll",
        "extension/tools/typewriter-lsp/Buildalyzer.Logger/net472/Buildalyzer.Logger.dll"
    )) {
        if ($requiredEntry -notin $entries) {
            throw "VS Code VSIX is missing expected entry: $requiredEntry"
        }
    }

    if ("extension/src/extension.js" -in $entries) {
        throw "VS Code VSIX unexpectedly contains source file: extension/src/extension.js"
    }

    $packagedScripts = @($entries | Where-Object {
        $_.StartsWith("extension/", [System.StringComparison]::Ordinal) -and
        $_.EndsWith(".ps1", [System.StringComparison]::OrdinalIgnoreCase)
    })

    if ($packagedScripts.Count -gt 0) {
        throw "VS Code VSIX unexpectedly contains PowerShell scripts: $($packagedScripts -join ', ')"
    }

    $toolExecutables = @($entries | Where-Object {
        $_.StartsWith("extension/tools/", [System.StringComparison]::Ordinal) -and
        $_.EndsWith(".exe", [System.StringComparison]::OrdinalIgnoreCase)
    })

    if ($toolExecutables.Count -gt 0) {
        throw "VS Code VSIX should package DLL-based tools only. Unexpected EXE entries: $($toolExecutables -join ', ')"
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Packaged VS Code extension:"
Write-Host $outputVsix
