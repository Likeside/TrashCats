#!/usr/bin/env bash
# Runs Unity tests in batchmode. The project must not be open in the Unity editor.
# Usage: Tools/run-tests.sh [EditMode|PlayMode|all] [results-dir]
set -euo pipefail

PLATFORM="${1:-all}"
PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
RESULTS_DIR="${2:-$PROJECT_DIR/TestResults}"
UNITY="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt")/Editor/Unity}"

mkdir -p "$RESULTS_DIR"
platforms=("$PLATFORM")
[[ "$PLATFORM" == "all" ]] && platforms=(EditMode PlayMode)

status=0
for p in "${platforms[@]}"; do
    xml="$RESULTS_DIR/$p.xml"
    rm -f "$xml"
    "$UNITY" -batchmode -nographics -projectPath "$PROJECT_DIR" \
        -runTests -testPlatform "$p" -testResults "$xml" -logFile "$RESULTS_DIR/$p.log" || status=1
    if grep -q "Scripts have compiler errors" "$RESULTS_DIR/$p.log" 2>/dev/null; then
        grep -E "error CS" "$RESULTS_DIR/$p.log" | sort -u | head -20
    fi
    summary=$(grep -o '<test-run [^>]*' "$xml" 2>/dev/null | grep -oE '(total|passed|failed)="[0-9]+"' | tr '\n' ' ' || true)
    echo "$p: ${summary:-no results (see $RESULTS_DIR/$p.log)}"
done
exit $status
