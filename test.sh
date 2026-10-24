#!/usr/bin/env bash
# Runs the core test suite with the per-user .NET SDK, printing only the summary and failure details.
# Exits non-zero when the build or any test fails, so `./test.sh && git commit` never commits red.
export DOTNET_ROOT="C:/Users/natha/AppData/Local/Microsoft/dotnet" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
"$DOTNET_ROOT/dotnet.exe" test tests/Cartheim.Core.Tests --nologo "$@" > /tmp/cartheim-test.log 2>&1
status=$?
grep -E "error [A-Z]+[0-9]+|^\s+Failed |Passed!|Failed!|Expected:|Actual:" /tmp/cartheim-test.log | sed -E 's/ \[D:[^]]*\]//' | awk '!seen[$0]++'
exit $status
