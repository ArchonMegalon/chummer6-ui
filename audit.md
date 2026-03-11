# Audit — ui

- Generated: 2026-03-08T08:51:33+01:00
- Repo root: /docker/chummercomplete/chummer-presentation
- Design doc expected: chummer-presentation.design.v2.md

## Solutions
Chummer.Presentation.sln
Chummer.sln
Chummer/Chummer.sln
Translator/Translator.sln

## Projects
Chummer.Avalonia.Browser/Chummer.Avalonia.Browser.csproj
Chummer.Avalonia/Chummer.Avalonia.csproj
Chummer.Benchmarks/Chummer.Benchmarks.csproj
Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj
Chummer.Blazor/Chummer.Blazor.csproj
Chummer.Desktop.Runtime/Chummer.Desktop.Runtime.csproj
Chummer.Presentation/Chummer.Presentation.csproj
Chummer.Tests/Chummer.Tests.csproj
Chummer/Chummer.csproj
ChummerDataViewer/ChummerDataViewer.csproj
CrashHandler/CrashHandler.csproj
Plugins/ChummerHub.Client/ChummerHub.Client.csproj
Plugins/ChummerHub.Client/OidcClient/IdentityTokenValidator/IdentityTokenValidator.csproj
Plugins/ChummerHub.Client/OidcClient/OidcClient/OidcClient.csproj
Plugins/SamplePlugin/SamplePlugin.csproj
TextblockConverter/Textblock-Converter.csproj
Translator/Translator.csproj

## Top-level directories
.aider.tags.cache.v4
.git
.github
Chummer
Chummer.Avalonia
Chummer.Avalonia.Browser
Chummer.Benchmarks
Chummer.Blazor
Chummer.Blazor.Desktop
Chummer.Desktop.Runtime
Chummer.Presentation
Chummer.Tests
ChummerDataViewer
CrashHandler
Docker
Plugins
TextblockConverter
Translator
docs
git
publish_output
scripts
settings

## Key instruction files
present: instructions.md
present: .agent-memory.md
present: AGENT_MEMORY.md
present: AGENTS.md
present: chummer-presentation.design.v2.md

## Recommendation
Keep all mechanics behind contracts. Presentation should consume packages and shared UI-kit seams only; shipped play/mobile heads belong in `chummer-play`, not this repo.
