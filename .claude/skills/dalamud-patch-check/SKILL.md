---
name: dalamud-patch-check
description: >-
  Check whether the latest Dalamud patch collides with the user's FFXIV/Dalamud
  plugins — API-level mismatches AND build breaks — and fix their own plugins
  (always Parcae/AutoFateGrind, Mercury and Fatum; also Minerva, Daedalus, Ariadne,
  Charon when present). Use when the user mentions a Dalamud update/patch, asks
  whether an update breaks or "collides with" their plugins, sees plugins marked
  outdated, a plugin stops building after an update, or wants to bump a plugin to a
  new Dalamud API level / Dalamud.NET.Sdk version.
---

# Dalamud patch collision check & fix

A Dalamud patch can collide with plugins in two ways, and this skill checks both:

1. **API-level mismatch (won't load).** Plugins declare a `DalamudApiLevel` (set by
   the `Dalamud.NET.Sdk` version they build against). When a patch bumps the API
   level, every plugin still declaring the old level is **disabled as "outdated"**
   until rebuilt against the new SDK.
2. **Build break at the same API level (won't compile).** A point release can ship a
   newer FFXIVClientStructs that breaks code compiled against the old one, with no
   API bump and no "outdated" flag. Real case: Dalamud **15.0.3.8** added
   `FFXIVClientStructs...ExcelPage`, making ECommons' `QuestDialogueText` ambiguous
   with `Lumina.Excel.ExcelPage` — Parcae stopped building until its ECommons
   submodule moved to upstream master. Only actually building the plugins catches this.

The user's own plugins that must **always** be checked: **Parcae**
(`C:/Dev/Parcae/AutoFateGrind/AutoFateGrind.csproj`), **Mercury**
(`C:/Dev/Mercury/Mercury.csproj`) and **Fatum** (`C:/Dev/Fatum/Fatum/Fatum.csproj`).
Minerva, Daedalus, Ariadne and Charon are checked too when their source is under
the dev roots.

API level == major of the Dalamud AssemblyVersion and of the `Dalamud.NET.Sdk`
version (e.g. `Dalamud.NET.Sdk/15.0.0` → API 15; Dalamud `15.0.3.x` → API 15).
Point releases within a major (15.0.3.6 → 15.0.3.7) do **not** change the API
level and never collide.

## 1. Determine the latest Dalamud

The installed/running version is the highest-numbered folder under
`%AppData%\XIVLauncher\addon\Hooks\<ver>\version.json`, and the script also fetches
the latest release and staging versions (the server answers in camelCase —
`assemblyVersion` — which the script handles). If its "latest" lines come back
empty (offline), fetch them with WebFetch:

- Release: `https://kamori.goats.dev/Dalamud/Release/VersionInfo?track=release`
- Staging: `https://kamori.goats.dev/Dalamud/Release/VersionInfo?track=staging`
- Latest SDK: `https://www.nuget.org/packages/Dalamud.NET.Sdk` (the package plugins
  compile against; its major is the API level a rebuilt plugin will declare)

Read `AssemblyVersion` and `SupportedGameVer` from each. A jump in the **major**
(e.g. 15 → 16) is an API-level bump → collisions are likely. A jump only in
`SupportedGameVer` at the same API level means the game patched but plugins still
load (they may still need a dependency refresh — see §4).

## 2. Run the collision check

Always run it with `--build` (the script lives beside this file, in `scripts/`; in
the Parcae repo that is `.claude/skills/dalamud-patch-check/scripts/`, in user skills
`~/.claude/skills/dalamud-patch-check/scripts/`):

```bash
bash <skill-dir>/scripts/check-dalamud.sh --build
```

It prints the installed Dalamud, the latest release/staging, and a table of every
**installed** plugin, every **dev** plugin (from `dalamudConfig.json`'s
DevPluginLoadLocations), and every **own-source** `*.csproj` under `C:/Dev` and the
Desktop, with each one's API level. Rows below the reference level are marked
`COLLIDES`. With `--build` it then compiles every own-source plugin (Parcae,
Mercury, Fatum, Minerva, …) with `--no-incremental`, in the configuration its dev
path loads from (`bin\Debug` → Debug, else Release), and reports `OK`/`FAILED` with
the errors, plus any **obsolete-API warnings** (CS0618/CS0612) in the plugin's own
code — those are Dalamud deprecations that will become breaks in a later patch.
Vendored ECommons warnings are ignored. If Parcae, Mercury or Fatum is missing from
the build table, its source moved — find it and build it by hand. Override scan
roots with `DEV_ROOTS="..."` and the XIVLauncher dir with `XL_BASE=...` if needed.

The reference level defaults to the **installed** Dalamud's major. When testing a
*newer* track the user intends to adopt, compare against that track's major instead
(from §1), since the installed copy hasn't moved yet.

## 3. Report

State plainly: the latest Dalamud version + API level, whether any plugin collides
on API level, and the build result for Parcae, Mercury and Fatum (and the others
built). If nothing is behind and everything builds, say "no collision — all plugins
are API N and build clean" and stop; do **not** invent a fix. Mention obsolete-API
warnings as early warnings even when the build passes. Call out separately any
plugin the user does **not** control (third-party installed plugins) that is behind —
those can only be fixed by their authors or a repo update, not by us.

## 4. Fix the user's own plugins (only when behind or failing, or on request)

Priority order: **Parcae**, **Mercury**, **Fatum**, then Minerva, Daedalus, Ariadne,
Charon if present under the dev roots.

**A build break at the same API level** (§2 shows `FAILED`, API unchanged): the
cause is almost always a vendored dependency compiled against the old
FFXIVClientStructs. For ECommons (a git submodule in Parcae), move it to upstream:
`git -C <repo>/ECommons fetch origin && git -C <repo>/ECommons checkout origin/master`,
rebuild, and commit the submodule bump. For NuGet deps (`croizat.clib` in Parcae),
bump to the newest version. If the error is in the plugin's own code, fix it there.

**An API-level bump**, for each plugin:

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
- A same-API patch can still break a plugin two ways: at **build** time (caught by
  `--build`, as with 15.0.3.8) or at **runtime** when a game patch moves memory
  offsets — that shows up as in-game errors / crashes, not an "outdated" flag or a
  build error. Both are fixed by the FFXIVClientStructs/ECommons refresh in §4, not an
  SDK major bump. `--build` cannot see runtime breaks; if the game itself patched
  (`SupportedGameVer` moved), tell the user to watch the Dalamud log after reloading.
