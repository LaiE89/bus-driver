#!/usr/bin/env bash
# Runs the Unity Editor in batch mode against BusDriver/. Extra arguments are passed through.
#   tools/unity.sh -quit -logFile Logs/compile.log
# UNITY_PATH overrides the Editor binary. Refuses to run while the Editor has the project open,
# because batch mode can't open a locked project (use the Unity CLI recompile path, ROADMAP §0.5).
set -u
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/BusDriver"
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity}"

# A crashed or aborted batch run leaves the file behind, so only a file some process holds open counts
LOCK="$PROJECT/Temp/UnityLockfile"
if [ -e "$LOCK" ] && { ! command -v lsof >/dev/null || [ -n "$(lsof -t "$LOCK" 2>/dev/null)" ]; }; then
    echo "unity.sh: the Editor has $PROJECT open (Temp/UnityLockfile exists)." >&2
    echo "  Close it, or compile through the Unity CLI instead:" >&2
    echo "  ~/.unity/bin/unity --no-banner command --project-path \"$PROJECT\" recompile" >&2
    exit 3
fi
if [ ! -x "$UNITY" ]; then
    echo "unity.sh: no Unity binary at $UNITY (set UNITY_PATH)" >&2
    exit 4
fi
mkdir -p "$PROJECT/Logs"
cd "$PROJECT" || exit 2
"$UNITY" -batchmode -projectPath "$PROJECT" "$@"
