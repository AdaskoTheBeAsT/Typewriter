param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version
)

$ErrorActionPreference = "Stop"

function Get-RepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    return Join-Path -Path $PSScriptRoot -ChildPath $RelativePath
}

function Save-XmlDocument {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlDocument]$Document,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $settings.Indent = $false
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::None
    $settings.OmitXmlDeclaration = $Document.FirstChild.NodeType -ne [System.Xml.XmlNodeType]::XmlDeclaration

    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $Document.Save($writer)
    } finally {
        $writer.Dispose()
    }
}

function Set-XmlNodeText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,
        [Parameter(Mandatory = $true)]
        [string]$XPath
    )

    $path = Get-RepositoryPath -RelativePath $RelativePath
    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $true
    $document.Load($path)

    $node = $document.SelectSingleNode($XPath)
    if ($null -eq $node) {
        throw "Unable to find XML node '$XPath' in '$RelativePath'."
    }

    $node.InnerText = $Version
    Save-XmlDocument -Document $document -Path $path
    Write-Host "Updated $RelativePath"
}

function Set-XmlAttributeValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,
        [Parameter(Mandatory = $true)]
        [string]$XPath,
        [Parameter(Mandatory = $true)]
        [string]$AttributeName
    )

    $path = Get-RepositoryPath -RelativePath $RelativePath
    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $true
    $document.Load($path)

    $node = $document.SelectSingleNode($XPath)
    if ($null -eq $node) {
        throw "Unable to find XML node '$XPath' in '$RelativePath'."
    }

    if ($null -eq $node.Attributes[$AttributeName]) {
        throw "Unable to find XML attribute '$AttributeName' in '$RelativePath'."
    }

    $node.SetAttribute($AttributeName, $Version)
    Save-XmlDocument -Document $document -Path $path
    Write-Host "Updated $RelativePath"
}

function Set-RegexValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,
        [Parameter(Mandatory = $true)]
        [string]$Pattern,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Replacement,
        [int]$ExpectedCount = 1
    )

    $path = Get-RepositoryPath -RelativePath $RelativePath
    $content = Get-Content -LiteralPath $path -Raw
    $regex = [regex]::new($Pattern)
    $matches = $regex.Matches($content)
    if ($matches.Count -ne $ExpectedCount) {
        throw "Expected $ExpectedCount match(es) for '$Pattern' in '$RelativePath', found $($matches.Count)."
    }

    $updated = $regex.Replace(
        $content,
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)
            return (& $Replacement $match)
        })

    Set-Content -LiteralPath $path -Value $updated -NoNewline
    Write-Host "Updated $RelativePath"
}

Set-XmlNodeText `
    -RelativePath "Directory.Build.props" `
    -XPath "/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='Version']"

Set-XmlAttributeValue `
    -RelativePath "src/Typewriter.VisualStudio/source.extension.vsixmanifest" `
    -XPath "/*[local-name()='PackageManifest']/*[local-name()='Metadata']/*[local-name()='Identity']" `
    -AttributeName "Version"

Set-RegexValue `
    -RelativePath "src/Typewriter.VisualStudio/TypewriterPackage.cs" `
    -Pattern '(productId:\s*")[^"]+(")' `
    -Replacement { param($match) "$($match.Groups[1].Value)$Version$($match.Groups[2].Value)" }

Set-RegexValue `
    -RelativePath "src/Typewriter.LanguageServer/LanguageServerHost.cs" `
    -Pattern '(\bversion\s*=\s*")[^"]+(",)' `
    -Replacement { param($match) "$($match.Groups[1].Value)$Version$($match.Groups[2].Value)" }

Set-RegexValue `
    -RelativePath "vscode/package.json" `
    -Pattern '(?s)^(\{\s*\r?\n\s*"name"\s*:\s*"typewriter-vscode",.*?\r?\n\s*"version"\s*:\s*")[^"]+(")' `
    -Replacement { param($match) "$($match.Groups[1].Value)$Version$($match.Groups[2].Value)" }

Set-RegexValue `
    -RelativePath "vscode/package-lock.json" `
    -Pattern '(?s)^(\{\s*\r?\n\s*"name"\s*:\s*"typewriter-vscode",\s*\r?\n\s*"version"\s*:\s*")[^"]+(")' `
    -Replacement { param($match) "$($match.Groups[1].Value)$Version$($match.Groups[2].Value)" }

Set-RegexValue `
    -RelativePath "vscode/package-lock.json" `
    -Pattern '(?s)("packages"\s*:\s*\{\s*\r?\n\s*""\s*:\s*\{\s*\r?\n\s*"name"\s*:\s*"typewriter-vscode",\s*\r?\n\s*"version"\s*:\s*")[^"]+(")' `
    -Replacement { param($match) "$($match.Groups[1].Value)$Version$($match.Groups[2].Value)" }

Set-RegexValue `
    -RelativePath "rider/gradle.properties" `
    -Pattern '(?m)^(pluginVersion=).*$' `
    -Replacement { param($match) "$($match.Groups[1].Value)$Version" }

Set-RegexValue `
    -RelativePath ".github/workflows/ci.yml" `
    -Pattern 'Typewriter\.VisualStudio-[0-9A-Za-z.+-]+\.vsix' `
    -Replacement { param($match) "Typewriter.VisualStudio-$Version.vsix" }

Set-RegexValue `
    -RelativePath ".github/workflows/ci.yml" `
    -Pattern 'typewriter-rider-[0-9A-Za-z.+-]+\.zip' `
    -Replacement { param($match) "typewriter-rider-$Version.zip" }

Set-RegexValue `
    -RelativePath ".github/workflows/ci.yml" `
    -Pattern 'Typewriter-Rider-[0-9A-Za-z.+-]+\.zip' `
    -Replacement { param($match) "Typewriter-Rider-$Version.zip" }

Write-Host "Version set to $Version"
