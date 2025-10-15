# Contribution rules for AI assistants (Codex)

For every push or PR, the CI generates **CODex-SUMMARY.md** and **codex-summary.json**.
AI contributors **must** ensure these files are up to date and accurately reflect:
- Changed files and diffs for `.sln` / `.csproj`.
- NuGet package changes (added/removed/updated versions).
- Build status (`dotnet build -c Release`) with key errors if any.
- Short notes about risky areas (XAML, controllers, telemetry, reports).

PRs without updated summaries may be asked to amend.
