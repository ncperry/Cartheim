#!/usr/bin/env bash
# Publishes a build for friends: tests, stamps the version, builds, packages, pushes and makes a GitHub release.
# Friends' Install.bat downloads Cartheim.dll and ValheimInventory.dll from the latest release (ValheimInventory's source
# stays private in ../ValheimInventory; only its DLL rides along); Cartheim-Installer.zip is the one-time download.
#   ./release.sh 0.2.0 "What changed"   publish
#   ./release.sh 0.2.0 --dry-run        build and package only; prints where the files are
set -euo pipefail
export DOTNET_ROOT="C:/Users/natha/AppData/Local/Microsoft/dotnet" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
repo=ncperry/Cartheim
valheim="D:/Steam/steamapps/common/Valheim"
inventory=../ValheimInventory
version=${1:-}
notes=${2:-}
dry_run=$([ "$notes" = "--dry-run" ] && echo 1 || true)

die() { echo "release: $*" >&2; exit 1; }
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "usage: ./release.sh <major.minor.patch> \"notes\" | --dry-run"
[ -n "$dry_run" ] || [ -z "$(git status --porcelain)" ] || die "commit or stash your changes first"
[ -n "$dry_run" ] || [ -z "$(git -C "$inventory" status --porcelain)" ] || die "commit or stash ValheimInventory's changes first"
[ -n "$dry_run" ] || ! git rev-parse -q --verify "refs/tags/v$version" >/dev/null || die "v$version is already released"

./test.sh
(cd "$inventory" && ./test.sh)

# The version Jotunn compares when a friend joins, and the DLL's own file version.
sed -i "s/public const string Version = \"[^\"]*\"/public const string Version = \"$version\"/" src/Cartheim.Plugin/Plugin.cs
"$DOTNET_ROOT/dotnet.exe" build src/Cartheim.Plugin -c Release -p:NoDeploy=true -p:Version="$version" > /tmp/cartheim-release.log 2>&1 ||
    { grep -E "error" /tmp/cartheim-release.log; die "build failed"; }
"$DOTNET_ROOT/dotnet.exe" build "$inventory/src/ValheimInventory.Plugin" -c Release -p:NoDeploy=true > /tmp/vi-release.log 2>&1 ||
    { grep -E "error" /tmp/vi-release.log; die "ValheimInventory build failed"; }

# The installer zip: Install.bat plus the BepInEx and Jotunn this game runs, so friends get the versions tested here.
out=$(mktemp -d)
stage="$out/Cartheim-Installer"
mkdir -p "$stage/payload/BepInEx/core" "$stage/payload/BepInEx/plugins"
cp installer/Install.bat installer/Uninstall.bat "$stage/"
cp "$valheim/winhttp.dll" "$valheim/doorstop_config.ini" "$valheim/.doorstop_version" "$stage/payload/"
cp "$valheim/BepInEx/core/"* "$stage/payload/BepInEx/core/"
cp "$valheim/BepInEx/plugins/Jotunn.dll" "$stage/payload/BepInEx/plugins/"
cp src/Cartheim.Plugin/bin/Release/netstandard2.1/Cartheim.dll "$out/"
cp "$inventory/src/ValheimInventory.Plugin/bin/Release/netstandard2.1/ValheimInventory.dll" "$out/"
(cd "$out" && /c/Windows/System32/tar.exe -a -cf Cartheim-Installer.zip Cartheim-Installer)

if [ -n "$dry_run" ]; then
    git checkout -q src/Cartheim.Plugin/Plugin.cs
    echo "Dry run: $out (Cartheim.dll, ValheimInventory.dll, Cartheim-Installer.zip)"
    exit 0
fi

git diff --quiet || git commit -q -am "Release v$version"
git tag "v$version"
git push -q origin HEAD "v$version"

# Git Credential Manager already holds a GitHub token from pushing; the release API takes the same one.
token=$(printf 'protocol=https\nhost=github.com\n\n' | git credential fill | sed -n 's/^password=//p')
api() { curl -fsS -H "Authorization: Bearer $token" -H "Accept: application/vnd.github+json" "$@"; }
notes="$notes

ValheimInventory build: $(git -C "$inventory" rev-parse --short HEAD)"
body=$(printf '%s' "$notes" | sed 's/\\/\\\\/g; s/"/\\"/g' | awk 'BEGIN{ORS="\\n"} 1')
release=$(api -X POST "https://api.github.com/repos/$repo/releases" \
    -d "{\"tag_name\":\"v$version\",\"name\":\"Cartheim $version\",\"body\":\"$body\"}")
upload=$(printf '%s' "$release" | sed -n 's/.*"upload_url": *"\([^{"]*\).*/\1/p' | head -1)
for file in Cartheim.dll ValheimInventory.dll Cartheim-Installer.zip; do
    api -X POST -H "Content-Type: application/octet-stream" --data-binary "@$out/$file" "$upload?name=$file" > /dev/null
done
echo "Released v$version: https://github.com/$repo/releases/tag/v$version"
echo "Friends' one-time download: https://github.com/$repo/releases/latest/download/Cartheim-Installer.zip"
