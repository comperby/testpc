param(
  [string]$SolutionPath = "ServiceBench.sln",
  [string]$AppCsproj    = "ServiceBench.App/ServiceBench.App.csproj"
)

$ErrorActionPreference = "SilentlyContinue"
$base = $env:GITHUB_BASE_SHA
if (-not $base -or $base -eq "") {
  if ($env:GITHUB_EVENT_NAME -eq "pull_request" -and $env:GITHUB_EVENT_PATH) {
    $event = Get-Content $env:GITHUB_EVENT_PATH | ConvertFrom-Json
    $base = $event.pull_request.base.sha
  }
}
if (-not $base -or $base -eq "") { $base = "HEAD~1" }
$head = "HEAD"

# 1) Изменённые файлы и дифы
$changed = git diff --name-status $base $head
$csprojDiff = git diff $base $head -- $AppCsproj
$slnDiff = git diff $base $head -- $SolutionPath
$xamlChanged = (git diff --name-only $base $head | Select-String -Pattern '\.xaml$' | ForEach-Object { $_.ToString() }) -join "`n"

# 2) Пакеты
dotnet restore $SolutionPath | Out-Null
dotnet list $SolutionPath package > packages.txt

# 3) Попытка сборки
$buildOk = $true
$buildLog = "build.log"
try {
  dotnet build $SolutionPath -c Release | Tee-Object -FilePath $buildLog
} catch {
  $buildOk = $false
}
$buildText = Get-Content $buildLog -Raw

# 4) Markdown summary
$ts = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
$md  = "# Change Summary ($ts)`n"
$md += "## Changed files`n``````" + "`n" + $changed + "`n``````" + "`n"
$md += "## NuGet packages (dotnet list package)`n``````" + "`n" + (Get-Content packages.txt -Raw) + "`n``````" + "`n"
$md += "## csproj diff`n``````" + "`n" + $csprojDiff + "`n``````" + "`n"
$md += "## sln diff`n``````" + "`n" + $slnDiff + "`n``````" + "`n"
if ($xamlChanged) { $md += "## XAML files changed`n``````" + "`n" + $xamlChanged + "`n``````" + "`n" }
$md += "## Build (Release)`n"
$md += ($buildOk ? "- Status: **OK**`n" : "- Status: **FAIL**`n")
$md += "``````" + "`n" + $buildText + "`n``````" + "`n"

Set-Content -Path "CODex-SUMMARY.md" -Value $md -Encoding UTF8

# 5) JSON summary
$json = [ordered]@{
  timestamp = (Get-Date).ToString("o")
  changed   = ($changed -split "`n" | Where-Object {$_ -ne ""})
  packages  = (Get-Content packages.txt -Raw)
  diffs     = @{
    csproj = $csprojDiff
    sln    = $slnDiff
    xaml   = $xamlChanged
  }
  build     = @{
    ok     = $buildOk
    log    = $buildText
  }
}
($json | ConvertTo-Json -Depth 6) | Set-Content -Path "codex-summary.json" -Encoding UTF8
