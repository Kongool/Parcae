#!/usr/bin/env bash
# Dalamud patch collision check (read-only).
# Reports: installed Dalamud, latest release/staging, and every plugin's DalamudApiLevel,
# flagging any plugin whose API level is behind the latest Dalamud API level.
#
# Usage: bash check-dalamud.sh [--build]
#   --build  also compile every own-source plugin (in the configuration its dev path loads from) against
#            the installed Dalamud, reporting failures and obsolete-API warnings. Catches same-API point
#            releases that still break the build (e.g. 15.0.3.8's FFXIVClientStructs ExcelPage vs ECommons).
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
    # The server answers in camelCase ("assemblyVersion"); the local version.json is PascalCase.
    v=$(echo "$j" | grep -ioE '"assemblyVersion":"[^"]*"' | sed 's/.*:"//;s/"//')
    g=$(echo "$j" | grep -ioE '"supportedGameVer":"[^"]*"' | sed 's/.*:"//;s/"//')
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
if [ "${1:-}" = "--build" ]; then
  echo "================ BUILD CHECK (own source) ================"
  # Each plugin is built in the configuration Dalamud loads it from (its bin/Debug or bin/Release dev path).
  DEVPATHS=$( [ -f "$CFG" ] && grep -oE '"Path":[[:space:]]*"[^"]+\.dll"' "$CFG" | sed 's/.*"\(.*\)"/\1/' | sed -E 's#\\+#/#g' )
  for root in $DEV_ROOTS; do
    [ -d "$root" ] || continue
    grep -rlE 'Sdk="Dalamud\.NET\.Sdk' "$root" --include=*.csproj 2>/dev/null | while read -r proj; do
      dir=$(dirname "$proj"); name=$(basename "$proj" .csproj)
      conf=Release
      # Here-strings, not `echo | grep -q`: under pipefail, grep -q quitting early SIGPIPEs the echo on big
      # output (Minerva's ~200 warnings) and the pipeline reads as failed even when the build succeeded.
      grep -qiF "$dir/bin/Debug/" <<<"$DEVPATHS" && conf=Debug
      out=$(dotnet build "$proj" -c "$conf" --no-incremental 2>&1)
      # Own code only: vendored ECommons warns plenty on its own and is not ours to fix.
      obsolete=$(echo "$out" | grep -E "warning CS06(18|12)" | grep -viE "[\/]ECommons[\/]" | sed -E 's/^[[:space:]]+//; s/ \[.*//' | sort -u)
      if grep -q "Build succeeded" <<<"$out"; then
        printf "  %-22s %-7s OK    obsolete-API warnings: %s\n" "$name" "$conf" "$( [ -n "$obsolete" ] && echo "$obsolete" | wc -l || echo 0)"
      else
        printf "  %-22s %-7s FAILED\n" "$name" "$conf"
        echo "$out" | grep -E " error " | sed -E 's/^[[:space:]]+//; s/ \[.*//' | sort -u | head -10 | sed 's/^/      /'
      fi
      [ -n "$obsolete" ] && echo "$obsolete" | head -5 | sed 's/^/      /'
    done
  done
  echo
fi

echo "Done. Any row marked COLLIDES has a DalamudApiLevel below the reference level and will be"
echo "disabled as 'outdated' after the patch; bump its Dalamud.NET.Sdk and rebuild (see SKILL.md)."
