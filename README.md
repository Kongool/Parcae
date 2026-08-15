<p align="center">
  <img src="AutoFateGrind/Images/Icon.png" width="180" alt="Parcae icon" />
</p>

<h1 align="center">Parcae</h1>

<p align="center">
  <a href="https://github.com/Kongool/Parcae/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/Kongool/Parcae?style=flat-square&color=blue"></a>
  <a href="https://github.com/Kongool/Parcae/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/Kongool/Parcae/total?style=flat-square&color=blue&cacheSeconds=300"></a>
  <a href="https://github.com/Kongool/Parcae/actions/workflows/release.yml"><img alt="Build" src="https://img.shields.io/github/actions/workflow/status/Kongool/Parcae/release.yml?style=flat-square"></a>
  <a href="LICENSE.md"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0--or--later-blue?style=flat-square"></a>
</p>

<p align="center">
  <em>Shape the route. Choose the goal. Let fate unwind.</em>
</p>

---

<p align="center">
  <img src="AutoFateGrind/Images/demo.gif" alt="Parcae demo" />
</p>

<p align="center">
  <strong>Example 24 hours full AFK run:</strong><br>
  <img src="AutoFateGrind/Images/Example2.png" alt="Example 24 hours full AFK run" />
</p>

## What it does

Parcae is a focused FATE operations console derived from [Auto FATE Grind](https://github.com/XeldarAlz/FFXIV-AutoFATEGrind) by XeldarAlz.

Lists every FATE zone from A Realm Reborn through Dawntrail in one window. Tick the zones you want, press **Run selected**, and the plugin teleports to each one, scans for active FATEs, flies to them, engages, and rotates to the next selected zone when the current one runs dry.

## Features

- **Zone picker**: pick any FATE zones from ARR through DT, with live active-FATE counts.
- **Five grind modes**: complete Shared FATE ranks by expansion, farm to a Gemstone target, run N FATEs, run for a set time, or go endless.
- **FATE filters & priority**: skip by type, time left, or progress, and reorder how the next FATE is chosen.
- **Live FATE tracker**: shown inline, or as a separate HUD overlay.
- **Class queue**: cycle gearsets in order with per-class level caps.
- **Auto-trade**: spends Bicolor Gemstones at the trader once you hit your threshold.
- **Auto-repair**: Dark Matter first, Grand Company mender as fallback.
- **Auto-consume**: keeps food and medicine buffs up (Well Fed is a free +3% EXP), HQ first.
- **Humanizer**: takes random city breaks between FATEs so long sessions look less mechanical.
- **Pause & resume**: park a run without losing your zones, goal, or session stats, and auto-pause while you're in a duty so you can queue for content mid-grind.
- **Party invites**: auto-declines incoming invites during a run after a random delay, with an optional reply message.
- **GM alert**: stops the bot when a GM is near, with optional toast, beeps, or custom commands.
- **Resilient**: cancellable mid-run, and your selection persists across reloads.

## Install

In-game: `/xlsettings` → **Experimental** → paste into **Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/Kongool/Parcae/main/repo.json
```

Tick **Enabled**, click **+**, then **Save and Close**. Open `/xlplugins` → **All Plugins**, search for **Parcae**, and install.

The plugin needs a few helpers for movement and combat to be installed and loaded. Open `/parcae deps` after install to see the list and one-click each missing one.

## Commands

| Command | Action |
|---|---|
| `/parcae` | Toggle the main window |
| `/afg` | Compatibility alias for `/parcae` |
| `/fategrind` | Compatibility alias for `/parcae` |
| `/parcae config` | Open settings |
| `/parcae deps` | Open dependencies window |
| `/parcae about` | Open credits / links |
| `/parcae pause` | Pause or resume the current run |
| `/parcae target` | Log targeted NPC's BaseId (debug helper) |

## Project

Source, issues, and releases are maintained at [Kongool/Parcae](https://github.com/Kongool/Parcae).

Original project: [XeldarAlz/FFXIV-AutoFATEGrind](https://github.com/XeldarAlz/FFXIV-AutoFATEGrind).

## License

AGPL-3.0-or-later. See [LICENSE.md](LICENSE.md).
