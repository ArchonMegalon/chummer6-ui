# Windows runtime content

Publish Avalonia with the approved Core Git repository supplied as a build input:

```powershell
dotnet publish Chummer.Avalonia/Chummer.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:ChummerCoreContentSource=C:/src/chummer6-core
```

The build uses Python (`-p:ChummerContentPython=python` by default) and reads immutable files from the approved Core commit. Dirty and untracked checkout files are excluded. Git, Python, the SDK and the source repository are build requirements only. Distribute the complete publish directory, including `core-content`, through both the installer and portable archive.

The manifest records every content file and its SHA-256. Its digest is embedded in the application assembly. Startup verifies the delivered manifest, every file and the exact file inventory. Missing, extra or changed content fails with an installation-repair message; it cannot fall back to an unrelated checkout. A moved portable directory keeps the same verification.

Unpublished development builds may use `CHUMMER_CORE_CONTENT_ROOT` or the existing pinned repository discovery. A published build always uses its bound content bundle, regardless of an inherited development override. Publishing without the verified bundle is rejected.

Verified locally: compiled bundle tests, deterministic Git-object export, actual Linux publish, moved published binaries loading without Git/environment lookup, and tampered-rule rejection. This is not clean-machine Windows, installer, signed-release or public-channel acceptance. Run the selected combined Windows candidate on a normal profile without Git, SDK, source repositories or content overrides; install, manually create, save, exit and reopen, and repeat after moving the portable directory.
