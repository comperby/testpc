<#
.SYNOPSIS
    Removes the legacy CodeTaskFactory-based MSBuild task from ServiceBench.App.csproj.

.DESCRIPTION
    Some older copies of the project still contain an inline MSBuild task that relies on
    CodeTaskFactory. MSBuild on modern .NET SDKs fails to load it with error MSB4801.
    This script deletes the obsolete blocks and replaces them with the supported attrib
    cleanup target that ships with the current repository version.

.PARAMETER ProjectPath
    Optional path to the csproj file. If omitted, the standard ServiceBench path is used.
#>
param(
    [string]$ProjectPath
)

$ErrorActionPreference = 'Stop'

if (-not $ProjectPath) {
    $ProjectPath = Join-Path -Path (Split-Path -Parent $PSScriptRoot) -ChildPath "ServiceBench.App/ServiceBench.App.csproj"
}

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Project file not found: $ProjectPath"
}

$content = Get-Content -LiteralPath $ProjectPath -Raw

if ($content -notmatch 'CodeTaskFactory' -and $content -match 'Exec Command="attrib') {
    Write-Host "Project already uses attrib-based cleanup."
    return
}

$usingTaskPattern = '<UsingTask[\s\S]*?</UsingTask>'
$clearTaskPattern = '<Target[^>]*?Name="EnsureObjWritable"[\s\S]*?<ClearReadOnlyAttributes[\s\S]*?</Target>'

$replacementTarget = @"
  <Target Name="EnsureObjWritable" BeforeTargets="ResolveReferences" Condition="'$(OS)' == 'Windows_NT'">
    <Exec Command="attrib -R &quot;$(BaseIntermediateOutputPath)*&quot; /S /D" IgnoreExitCode="true" />
  </Target>
"@

$updated = [System.Text.RegularExpressions.Regex]::Replace($content, $usingTaskPattern, '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
$updated = [System.Text.RegularExpressions.Regex]::Replace($updated, $clearTaskPattern, $replacementTarget.Trim(), [System.Text.RegularExpressions.RegexOptions]::Singleline)

if ($updated -eq $content) {
    Write-Warning "No inline task blocks were changed."
    return
}

Set-Content -LiteralPath $ProjectPath -Value $updated -Encoding UTF8
Write-Host "Updated $ProjectPath to remove CodeTaskFactory usage."
