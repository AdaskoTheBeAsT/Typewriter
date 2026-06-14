[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [string] $OutputDirectory,

    [switch] $NoRestore,

    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$projectPath = Join-Path $repoRoot "src/Typewriter.Cli/Typewriter.Cli.csproj"
$directoryBuildPropsPath = Join-Path $repoRoot "Directory.Build.props"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts"
}

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

function Get-ProjectProperty {
    param(
        [Parameter(Mandatory = $true)]
        [xml] $Project,

        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    $node = $Project.SelectSingleNode("/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='$Name']")
    if ($null -eq $node) {
        return $null
    }

    return $node.InnerText.Trim()
}

[xml] $project = Get-Content -LiteralPath $projectPath
[xml] $directoryBuildProps = Get-Content -LiteralPath $directoryBuildPropsPath
$packageId = Get-ProjectProperty $project "PackageId"
$version = Get-ProjectProperty $directoryBuildProps "Version"
$targetFramework = Get-ProjectProperty $project "TargetFramework"
$assemblyName = Get-ProjectProperty $project "AssemblyName"

if ([string]::IsNullOrWhiteSpace($packageId)) {
    throw "Unable to read PackageId from $projectPath."
}

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Unable to read Version from $directoryBuildPropsPath."
}

if ([string]::IsNullOrWhiteSpace($targetFramework)) {
    throw "Unable to read TargetFramework from $projectPath."
}

if ([string]::IsNullOrWhiteSpace($assemblyName)) {
    $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$packArguments = @(
    "pack",
    $projectPath,
    "--configuration", $Configuration,
    "--output", $OutputDirectory
)

if ($NoRestore) {
    $packArguments += "--no-restore"
}

if ($NoBuild) {
    $packArguments += "--no-build"
}

Invoke-ExternalCommand "dotnet" $packArguments

$packagePath = Join-Path $OutputDirectory "$packageId.$version.nupkg"
if (-not (Test-Path -LiteralPath $packagePath)) {
    throw "CLI NuGet tool package was not produced: $packagePath"
}

if ((Get-Item -LiteralPath $packagePath).Length -le 0) {
    throw "CLI NuGet tool package is empty: $packagePath"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $entries = @($zip.Entries | ForEach-Object { $_.FullName.Replace("\", "/") })

    foreach ($requiredEntry in @(
        "tools/$targetFramework/any/DotnetToolSettings.xml",
        "tools/$targetFramework/any/$assemblyName.dll"
    )) {
        if ($requiredEntry -notin $entries) {
            throw "CLI NuGet tool package is missing expected entry: $requiredEntry"
        }
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Packaged CLI NuGet tool:"
Write-Host $packagePath
