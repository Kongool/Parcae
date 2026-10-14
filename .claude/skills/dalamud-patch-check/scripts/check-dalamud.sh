#!/usr/bin/env bash
# Dalamud patch collision check (read-only).
# Reports: installed Dalamud, latest release/staging, and every plugin's DalamudApiLevel,
# flagging any plugin whose API level is behind the latest Dalamud API level.
#
# Usage: bash check-dalamud.sh
# Env overrides: XL_BASE (XIVLauncher roaming dir), DEV_ROOTS (space-separated source roots).
set -uo pipefail

XL_BASE="${XL_BASE:-$APPDATA/XIVLauncher}"
[ -d "$XL_BASE" ] || XL_BASE="C:/Users/$USER/AppData/Roaming/XIVLauncher"
HOOKS="$XL_BASE/addon/Hooks"
CFG="$XL_BASE/dalamudConfig.json"

api_of_version() { echo "$1" | cut -d. -f1; }   # Dalamud API level == AssemblyVersion major (SDK 15 -> API 15)

echo "===================== DALAMUD ====================="
INSTALLED_VER="$(ls -1 "$HOOKS" 2>/dev/null | grep -E '^[0-9]' | sort -V | tail -1)"
if [ -n "$INSTALLED_VER" ] && [ -f "$HOOKS/$INSTALLED_VER/version.json" ]; then
  gamever=$(grep -oE '"SupportedGameVer":"[^"]*"' "$HOOKS/$INSTALLED_VER/version.json" | sed 's/.*:"//;s/"//')
  track=$(grep -oE '"Track":"[^"]*"'            "$HOOKS/$INSTALLED_VER/version.json" | sed 's/.*:"//;s/"//')
  echo "installed : $INSTALLED_VER  (API $(api_of_version "$INSTALLED_VER")) track=$track game=$gamever"
else
  echo "installed : (none found under $HOOKS)"
fi

# Latest from the distribution server (release + staging). Needs network; skipped gracefully if offline.
for t in release staging; do
  j=$(curl -fsS "https://kamori.goats.dev/Dalamud/Release/VersionInfo?track=$t" 2>/dev/null)
  if [ -n "$j" ]; then
    v=$(echo "$j" | grep -oE '"AssemblyVersion":"[^"]*"' | sed 's/.*:"//;s/"//')
    g=$(echo "$j" | grep -oE '"SupportedGameVer":"[^"]*"' | sed 's/.*:"//;s/"//')
    echo "latest $t : $v  (API $(api_of_version "$v")) game=$g"
  else
    echo "latest $t : (network unavailable — check https://kamori.goats.dev/Dalamud/Release/VersionInfo?track=$t)"
  fi
done

LATEST_API="$(api_of_version "${INSTALLED_VER:-0}")"
echo "latest Dalamud.NET.Sdk on NuGet: https://www.nuget.org/packages/Dalamud.NET.Sdk  (its major == target API level)"
echo "reference API level for collision test: $LATEST_API (installed). Override by editing LATEST_API if a newer track is being adopted."
echo

print_row() { # name  api  source
  local nm="$1" api="$2" src="$3" flag="ok"
  [ -z "$api" ] && api="?"
  if [ "$api" != "?" ] && [ "$api" -lt "$LATEST_API" ] 2>/dev/null; then flag="COLLIDES (<$LATEST_API)"; fi
  printf "  %-24s api=%-4s %-9s %s\n" "$nm" "$api" "$flag" "$src"
}

echo "================= INSTALLED PLUGINS ================"
find "$XL_BASE/installedPlugins" -maxdepth 3 -iname "*.json" 2>/dev/null | while read -r f; do
  base=$(basename "$f" .json); dir=$(basename "$(dirname "$(dirname "$f")")")
  [ "$base" = "$dir" ] || continue   # the manifest is <Name>/<ver>/<Name>.json
  nm=$(grep -oE '"InternalName"[^,]*' "$f" | head -1 | sed 's/.*: *"//;s/".*//')
  api=$(grep -oE '"DalamudApiLevel"[^,]*' "$f" | head -1 | grep -oE '[0-9]+')
  print_row "${nm:-$base}" "$api" "installed"
done | sort -u

echo
echo "=================== DEV PLUGINS ==================="
if [ -f "$CFG" ]; then
  grep -oE '"Path":[[:space:]]*"[^"]+\.dll"' "$CFG" | sed 's/.*"\(.*\)"/\1/' | sed 's#\\\\#/#g' | while read -r dll; do
    man="${dll%.dll}.json"
    [ -f "$man" ] || continue
    nm=$(grep -oE '"InternalName"[^,]*' "$man" | head -1 | sed 's/.*: *"//;s/".*//')
    api=$(grep -oE '"DalamudApiLevel"[^,]*' "$man" | head -1 | grep -oE '[0-9]+')
    print_row "${nm:-$(basename "$man" .json)}" "$api" "dev: $dll"
  done
else
  echo "  (no dalamudConfig.json at $CFG)"
fi

echo
echo "=============== OWN SOURCE (csproj SDK) ==========="
# Space-separated roots to scan for *.csproj using Dalamud.NET.Sdk.
DEV_ROOTS="${DEV_ROOTS:-C:/Dev $HOME/Desktop}"
for root in $DEV_ROOTS; do
  [ -d "$root" ] || continue
  grep -rlE 'Sdk="Dalamud\.NET\.Sdk' "$root" --include=*.csproj 2>/dev/null | while read -r proj; do
    sdkver=$(grep -oE 'Dalamud\.NET\.Sdk/[0-9][0-9.]*' "$proj" | head -1 | cut -d/ -f2)
    api=$(api_of_version "$sdkver")
    print_row "$(basename "$proj")" "$api" "sdk $sdkver  $proj"
  done
done
echo
echo "Done. Any row marked COLLIDES has a DalamudApiLevel below the reference level and will be"
echo "disabled as 'outdated' after the patch; bump its Dalamud.NET.Sdk and rebuild (see SKILL.md)."
