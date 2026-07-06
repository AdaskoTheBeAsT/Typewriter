[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [string] $Framework = "net10.0",

    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$riderRoot = Join-Path $repoRoot "rider"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts"
}

$gradleWrapper = if ($IsWindows) {
    Join-Path $riderRoot "gradlew.bat"
}
else {
    Join-Path $riderRoot "gradlew"
}

$toolRoot = Join-Path $riderRoot "build/typewriter-tools"
$cliPublishDir = Join-Path $toolRoot "typewriter-cli"
$languageServerPublishDir = Join-Path $toolRoot "typewriter-lsp"
$gradlePropertiesPath = Join-Path $riderRoot "gradle.properties"

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

function Get-PluginVersion {
    $versionLine = Get-Content -LiteralPath $gradlePropertiesPath |
        Where-Object { $_ -match "^\s*pluginVersion\s*=" } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($versionLine)) {
        throw "Unable to read pluginVersion from $gradlePropertiesPath."
    }

    return ($versionLine -replace "^\s*pluginVersion\s*=\s*", "").Trim()
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
        throw "Published Rider tools must be DLL-based and cross-platform. Unexpected EXE files: $($nativeLaunchers.FullName -join ', ')"
    }
}

function Add-DirectoryToZip {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Compression.ZipArchive] $Zip,

        [Parameter(Mandatory = $true)]
        [string] $SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string] $EntryPrefix,

        [switch] $KeepTypewriterXmlDocFiles
    )

    $resolvedSourceDirectory = (Resolve-Path -LiteralPath $SourceDirectory).Path
    $files = Get-ChildItem -LiteralPath $resolvedSourceDirectory -Recurse -File |
        Where-Object {
            $_.Extension -notin @(".pdb", ".xml") -or
            ($KeepTypewriterXmlDocFiles -and $_.Name -like "Typewriter.*.xml")
        }

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

if (-not (Test-Path -LiteralPath $gradleWrapper)) {
    throw "Gradle wrapper not found: $gradleWrapper"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$pluginVersion = Get-PluginVersion
$pluginZip = Join-Path $riderRoot "build/distributions/typewriter-rider-$pluginVersion.zip"
$outputZip = Join-Path $OutputDirectory "Typewriter-Rider-$pluginVersion.zip"

Publish-FrameworkDependentTool `
    -ProjectPath (Join-Path $repoRoot "src/Typewriter.Cli/Typewriter.Cli.csproj") `
    -OutputPath $cliPublishDir `
    -AssemblyName "Typewriter.Cli"

Publish-FrameworkDependentTool `
    -ProjectPath (Join-Path $repoRoot "src/Typewriter.LanguageServer/Typewriter.LanguageServer.csproj") `
    -OutputPath $languageServerPublishDir `
    -AssemblyName "Typewriter.LanguageServer"

Invoke-ExternalCommand $gradleWrapper @("-p", $riderRoot, "buildPlugin")

if (-not (Test-Path -LiteralPath $pluginZip)) {
    throw "Rider plugin ZIP was not produced: $pluginZip"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($pluginZip, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    $toolEntryPrefixes = @(
        "typewriter-rider/tools/typewriter-cli/",
        "typewriter-rider/tools/typewriter-lsp/"
    )

    $entriesToDelete = @($zip.Entries | Where-Object {
        $entryName = $_.FullName
        $toolEntryPrefixes |
            Where-Object { $entryName.StartsWith($_, [System.StringComparison]::Ordinal) } |
            Select-Object -First 1
    })

    foreach ($entry in $entriesToDelete) {
        $entry.Delete()
    }

    Add-DirectoryToZip $zip $cliPublishDir "typewriter-rider/tools/typewriter-cli"
    # XML doc files are required so Roslyn hover in .tst C# blocks can show doc comments.
    Add-DirectoryToZip $zip $languageServerPublishDir "typewriter-rider/tools/typewriter-lsp" -KeepTypewriterXmlDocFiles
}
finally {
    $zip.Dispose()
}

Copy-Item -LiteralPath $pluginZip -Destination $outputZip -Force

if (-not (Test-Path -LiteralPath $outputZip)) {
    throw "Rider plugin ZIP was not copied to: $outputZip"
}

if ((Get-Item -LiteralPath $outputZip).Length -le 0) {
    throw "Rider plugin ZIP is empty: $outputZip"
}

$outputArchive = [System.IO.Compression.ZipFile]::OpenRead($outputZip)
try {
    $entries = @($outputArchive.Entries | ForEach-Object { $_.FullName.Replace("\", "/") })

    foreach ($requiredEntry in @(
        "typewriter-rider/tools/typewriter-cli/Typewriter.Cli.dll",
        "typewriter-rider/tools/typewriter-cli/Typewriter.Cli.deps.json",
        "typewriter-rider/tools/typewriter-cli/Typewriter.Cli.runtimeconfig.json",
        "typewriter-rider/tools/typewriter-cli/Buildalyzer.Logger.dll",
        "typewriter-rider/tools/typewriter-cli/Buildalyzer.Logger/net472/Buildalyzer.Logger.dll",
        "typewriter-rider/tools/typewriter-lsp/Typewriter.LanguageServer.dll",
        "typewriter-rider/tools/typewriter-lsp/Typewriter.LanguageServer.deps.json",
        "typewriter-rider/tools/typewriter-lsp/Typewriter.LanguageServer.runtimeconfig.json",
        "typewriter-rider/tools/typewriter-lsp/Typewriter.Engine.xml",
        "typewriter-rider/tools/typewriter-lsp/Typewriter.Abstractions.xml",
        "typewriter-rider/tools/typewriter-lsp/Buildalyzer.Logger.dll",
        "typewriter-rider/tools/typewriter-lsp/Buildalyzer.Logger/net472/Buildalyzer.Logger.dll"
    )) {
        if ($requiredEntry -notin $entries) {
            throw "Rider plugin ZIP is missing expected entry: $requiredEntry"
        }
    }

    $toolExecutables = @($entries | Where-Object {
        $_.StartsWith("typewriter-rider/tools/", [System.StringComparison]::Ordinal) -and
        $_.EndsWith(".exe", [System.StringComparison]::OrdinalIgnoreCase)
    })

    if ($toolExecutables.Count -gt 0) {
        throw "Rider plugin ZIP should package DLL-based tools only. Unexpected EXE entries: $($toolExecutables -join ', ')"
    }
}
finally {
    $outputArchive.Dispose()
}

Write-Host "Packaged Rider plugin:"
Write-Host $outputZip
