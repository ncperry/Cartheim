#!/usr/bin/env bash
# Builds the merged plugin DLL; the build copies it into BepInEx\scripts, where ScriptEngine hot-reloads it.
# Exits non-zero when the build fails, so `./deploy.sh && git commit` never commits a broken build.
export DOTNET_ROOT="C:/Users/natha/AppData/Local/Microsoft/dotnet" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
"$DOTNET_ROOT/dotnet.exe" build src/Cartheim.Plugin "$@" > /tmp/cartheim-build.log 2>&1
status=$?
grep -vE "csc.dll|MSB3277" /tmp/cartheim-build.log | grep -E "error|warning CS|Build succeeded" | sed -E 's/ \[D:[^]]*\]//' | awk '!seen[$0]++'
exit $status
