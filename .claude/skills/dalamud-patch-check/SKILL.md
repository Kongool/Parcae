---
name: dalamud-patch-check
description: >-
  Check whether the latest Dalamud patch collides with the user's FFXIV/Dalamud
  plugins, and bump + rebuild their own plugins (Parcae/AutoFateGrind, Mercury,
  Minerva, Daedalus, Ariadne, Fatum, Charon) when the Dalamud API level moves.
  Use when the user mentions a Dalamud update/patch, asks whether an update
  breaks or "collides with" their plugins, sees plugins marked outdated, or wants
  to bump a plugin to a new Dalamud API level / Dalamud.NET.Sdk version.
---

# Dalamud patch collision check & fix

Dalamud plugins declare a `DalamudApiLevel` (set by the `Dalamud.NET.Sdk` version
they build against). When a Dalamud patch bumps the API level, every plugin still
declaring the old level is **disabled as "outdated"** until rebuilt against the new
SDK. That API-level mismatch is the "collision." This skill detects it and fixes
the user's own plugins.

API level == major of the Dalamud AssemblyVersion and of the `Dalamud.NET.Sdk`
version (e.g. `Dalamud.NET.Sdk/15.0.0` → API 15; Dalamud `15.0.3.x` → API 15).
Point releases within a major (15.0.3.6 → 15.0.3.7) do **not** change the API
level and never collide.

## 1. Determine the latest Dalamud

The installed/running version is the highest-numbered folder under
`%AppData%\XIVLauncher\addon\Hooks\<ver>\version.json` (the `check-dalamud.sh`
script reads it). For the **latest available** version the script tries `curl`,
but Git Bash curl often fails on these endpoints — if the script's "latest"
lines come back empty, fetch them yourself with WebFetch:

- Release: `https://kamori.goats.dev/Dalamud/Release/VersionInfo?track=release`
- Staging: `https://kamori.goats.dev/Dalamud/Release/VersionInfo?track=staging`
- Latest SDK: `https://www.nuget.org/packages/Dalamud.NET.Sdk` (the package plugins
  compile against; its major is the API level a rebuilt plugin will declare)

Read `AssemblyVersion` and `SupportedGameVer` from each. A jump in the **major**
(e.g. 15 → 16) is an API-level bump → collisions are likely. A jump only in
`SupportedGameVer` at the same API level means the game patched but plugins still
load (they may still need a dependency refresh — see §4).

## 2. Run the collision check

```bash
bash .claude/skills/dalamud-patch-check/scripts/check-dalamud.sh
```

It prints the installed Dalamud, the latest release/staging, and a table of every
**installed** plugin, every **dev** plugin (from `dalamudConfig.json`'s
DevPluginLoadLocations), and every **own-source** `*.csproj` under `C:/Dev` and the
Desktop, with each one's API level. Rows below the reference level are marked
`COLLIDES`. Override scan roots with `DEV_ROOTS="..."` and the XIVLauncher dir with
`XL_BASE=...` if they differ.

The reference level defaults to the **installed** Dalamud's major. When testing a
*newer* track the user intends to adopt, compare against that track's major instead
(from §1), since the installed copy hasn't moved yet.

## 3. Report

State plainly: the latest Dalamud version + API level, and whether any plugin
collides. If nothing is behind the reference level, say "no collision — all plugins
are API N" and stop; do **not** invent a fix. Call out separately any plugin the
user does **not** control (third-party installed plugins) that is behind — those can
only be fixed by their authors or a repo update, not by us.

## 4. Fix the user's own plugins (only when behind, or on request)

The user's own source projects (bump these, in priority order): **Parcae**
(`C:/Dev/Parcae/AutoFateGrind/AutoFateGrind.csproj`) and **Mercury**
(`C:/Dev/Mercury/Mercury.csproj`), then Minerva, Daedalus, Ariadne, Fatum, Charon
if present under the dev roots. For each:

1. **Bump the SDK** in the `.csproj`: `Sdk="Dalamud.NET.Sdk/<old>"` →
   `Sdk="Dalamud.NET.Sdk/<new-major>.0.0"` (the `DalamudApiLevel` in the built
   manifest is derived from the SDK — do not hand-edit the `.json`).
2. **Refresh game-coupled dependencies** if the game patched: these plugins pin
   `ECommons` (submodule under the repo) and `croizat.clib` (NuGet, e.g.
   `croizat.clib` in Parcae) and transitively FFXIVClientStructs via the SDK. On an
   API/game bump, update the ECommons submodule to a commit built for the new API
   and bump `croizat.clib` to its matching version (check its NuGet page), then
   `dotnet restore`.
3. **Rebuild** and fix any compile breaks from the new API:
   ```bash
   dotnet build <proj> -c Release   # and -c Debug for dev-loaded plugins
   ```
   Dev-loaded plugins (Parcae, Mercury, Minerva, …) load from their `bin\Debug` or
   `bin\Release` per `dalamudConfig.json`; build the configuration that path points
   at so the loaded copy updates. Parcae also deploys to its dev-plugin path on
   build — rebuild Debug there.
4. **Re-run the check** (§2) to confirm the rebuilt plugin now reports the new API
   level, and have the user reload dev plugins in Dalamud (Settings → Experimental)
   or restart the game.

Commit each repo on its own; end commit messages with the attribution line the
session requires.

## Notes

- Staging/testing Dalamud bumps the API level **before** release. If the user runs
  the staging track, fix proactively; if release, they have until release to bump.
- "Collides" strictly means API-level mismatch (won't load). A same-API game patch
  that breaks a plugin's memory offsets is a *runtime* break, not a load collision —
  it shows up as in-game errors / crashes, not an "outdated" flag, and is fixed by
  the FFXIVClientStructs/ECommons refresh in §4.2, not an SDK major bump.
