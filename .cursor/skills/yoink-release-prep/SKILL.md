---
name: yoink-release-prep
description: Bumps the semantic version in src/Yoink/Yoink.csproj and runs scripts/build.ps1 to publish a self-contained exe and zip under dist/ for production. Use when preparing a production release, shipping a build, bumping the app version, or running the release build pipeline locally.
---

# Yoink production release prep

## When to use

Apply this workflow when the user wants to **increment the version** and **produce production artifacts** (published exe + zip) for Yoink.

## Prerequisites

- .NET 9 SDK
- Windows PowerShell 5.1+ (repo is Windows-first; `build.ps1` uses PowerShell)

## Steps

### 1. Bump version

- Open `src/Yoink/Yoink.csproj` and find `<Version>x.y.z</Version>` (SDK-style; first `PropertyGroup` with `Version` is enough).
- **Default:** increment **patch** (`1.0.5` -> `1.0.6`).
- If the user asks for a **minor** or **major** bump, adjust that segment and zero the lower segments as usual (e.g. `1.0.6` -> `1.1.0` or `2.0.0`).
- If the user gives an **exact version**, set `<Version>` to that string instead.
- Save the file. The zip name will use this version when the build script runs.

### 2. Run the build script

From the **repository root**:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
```

- **Default RID** is `win-x64`. For other targets: `-Rid win-x86` or `-Rid win-arm64`.
- **Tests** run before publish by default. Use **`-SkipTests`** only when the user explicitly wants to skip them (e.g. known failing test, emergency build).

### 3. Confirm outputs

- Published folder: `release/` (or `-PublishDir` if overridden).
- Zip artifact: `dist/Yoink-{version}-{rid}.zip` (version read from the csproj by the script).

Report the **new version**, **publish path**, and **zip path** to the user.

## Optional follow-ups

- If the repo maintains `RELEASE_NOTES.md` or similar, ask whether to add an entry for the new version (do not assume).
- Do not commit or tag unless the user asks; this skill stops at version bump + build artifacts.

## Pitfalls

- Bump **before** running `build.ps1`; the script reads `<Version>` from the csproj for the zip filename.
- Keep `build.ps1` and `Write-Host` strings **ASCII-only** if editing the script (Unicode punctuation can break Windows PowerShell 5.1 parsing).
