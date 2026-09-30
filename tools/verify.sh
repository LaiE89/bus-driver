#!/usr/bin/env bash
# Headless verification (ROADMAP §0.5). Run from anywhere:  tools/verify.sh <mode>
#   quick     compile, then EditMode tests
#   content   content builders, then EditMode tests
#   playmode  PlayMode tests
#   smoke     the smoke test (captures in BusDriver/Logs/smoke/*.png)
#   build     standalone build for the current OS, then its --selftest
#   full      content + playmode + smoke + build
# Exits non-zero with a one-line reason on the first failure. UNITY_PATH overrides the Editor.
set -u
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/BusDriver"
LOGS="$PROJECT/Logs"
UNITY_SH="$ROOT/tools/unity.sh"
mkdir -p "$LOGS"

fail() {
    echo "verify: FAIL ($MODE): $*" >&2
    exit 1
}

step() {
    echo "verify: $*"
}

# Runs Unity through unity.sh; a lock or missing binary is reported as it is
unity() {
    "$UNITY_SH" "$@"
    local code=$?
    if [ "$code" -eq 3 ] || [ "$code" -eq 4 ]; then
        exit "$code"
    fi
    return "$code"
}

# Compile errors, exceptions and builder SetRef misses, ignoring the Editor's own Search indexer
# exception at startup (an Editor bug, not ours) and the benign licensing curl noise
check_log() {
    local log="$1"
    [ -f "$log" ] || fail "no log at $log"
    local problems
    problems=$(python3 - "$log" <<'EOF'
import re, sys
lines = open(sys.argv[1], errors='ignore').read().split('\n')
bad = []
for i, line in enumerate(lines):
    if re.search(r'error CS\d+|no serialized field', line):
        bad.append(line.strip())
    # A thrown exception is logged as "SomeException: message" (file lists such as the build
    # report's size table only mention *Exception.cs)
    elif re.search(r'\b\w*Exception: ', line) and not re.search(r'^\s+at |^\s*UnityEngine\.|StackTraceUtility|Curl error', line):
        context = '\n'.join(lines[i:i + 8])
        if 'UnityEditor.Search' in context or 'LogAssert' in context:
            continue
        # Exceptions a test deliberately expects are logged as part of its own output
        if re.search(r'Expected (log|exception)', context):
            continue
        bad.append(line.strip())
seen = []
for b in bad:
    if b not in seen:
        seen.append(b)
print('\n'.join(seen[:10]))
EOF
)
    if [ -n "$problems" ]; then
        echo "$problems" >&2
        fail "problems in $(basename "$log") (first: $(echo "$problems" | head -1))"
    fi
}

# Test results: any failed case fails the step, naming the tests
check_results() {
    local xml="$1" label="$2"
    [ -f "$xml" ] || fail "$label produced no results file ($xml); see the log"
    local summary
    summary=$(python3 - "$xml" <<'EOF'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
failed = [c.get('fullname') for c in root.iter('test-case') if c.get('result') == 'Failed']
total, passed = root.get('total', '0'), root.get('passed', '0')
if failed:
    print('FAILED ' + ', '.join(failed))
else:
    print(f'OK {passed}/{total} passed')
EOF
)
    case "$summary" in
        OK*) step "$label: ${summary#OK }" ;;
        *) fail "$label: ${summary#FAILED }" ;;
    esac
}

run_compile() {
    step "compile"
    unity -quit -logFile "$LOGS/compile.log" || { check_log "$LOGS/compile.log"; fail "compile exited non-zero"; }
    check_log "$LOGS/compile.log"
}

run_editmode() {
    step "EditMode tests"
    rm -f "$LOGS/editmode.xml"
    unity -runTests -testPlatform EditMode -testResults "$LOGS/editmode.xml" -logFile "$LOGS/editmode.log"
    check_results "$LOGS/editmode.xml" "EditMode"
    check_log "$LOGS/editmode.log"
}

run_playmode() {
    step "PlayMode tests"
    rm -f "$LOGS/playmode.xml"
    unity -runTests -testPlatform PlayMode -testResults "$LOGS/playmode.xml" -logFile "$LOGS/playmode.log"
    check_results "$LOGS/playmode.xml" "PlayMode"
    check_log "$LOGS/playmode.log"
}

# Generated content (BuildAll's output, §4.15). Hand edits to generated files would be lost, so
# refuse while they have uncommitted changes (VERIFY_ALLOW_DIRTY=1 overrides)
GENERATED=(
    "BusDriver/Assets/Generated"
)

# Hashes of every generated file as the last content run left it. A dirty file that still matches
# its stamp is our own builder output (fileIDs change on every rebuild), not a hand edit.
CONTENT_STAMP="$LOGS/content.stamp"

generated_hashes() {
    (cd "$ROOT" && find "${GENERATED[@]}" -type f 2>/dev/null | sort | while IFS= read -r f; do
        printf '%s %s\n' "$(git hash-object "$f")" "$f"
    done)
}

run_content() {
    if [ "${VERIFY_ALLOW_DIRTY:-0}" != "1" ] && command -v git >/dev/null; then
        local dirty path hand_edited=""
        dirty=$(cd "$ROOT" && git status --porcelain -uall -- "${GENERATED[@]}" 2>/dev/null | cut -c4- | sed 's/^"//;s/"$//')
        while IFS= read -r path; do
            [ -n "$path" ] || continue
            if [ -f "$ROOT/$path" ] && [ -f "$CONTENT_STAMP" ] \
                && grep -qxF "$(cd "$ROOT" && git hash-object "$path") $path" "$CONTENT_STAMP"; then
                continue
            fi
            hand_edited="$hand_edited$path"$'\n'
        done <<< "$dirty"
        if [ -n "$hand_edited" ]; then
            printf '%s' "$hand_edited" >&2
            fail "generated files have uncommitted changes that no content run produced; commit or discard them first (a rebuild would overwrite them), or set VERIFY_ALLOW_DIRTY=1"
        fi
    fi
    local method=BusDriver.Editor.Builders.BuildAll.Run
    step "content: $method"
    unity -executeMethod "$method" -quit -logFile "$LOGS/content.log" || { check_log "$LOGS/content.log"; fail "$method exited non-zero"; }
    check_log "$LOGS/content.log"
    generated_hashes > "$CONTENT_STAMP"
    run_editmode
}

run_smoke() {
    step "smoke test"
    mkdir -p "$LOGS/smoke"
    unity -executeMethod BusDriver.Editor.Smoke.SmokeTest.Run -logFile "$LOGS/smoke.log"
    local code=$?
    local result
    result=$(grep '^\[SMOKE\] RESULT' "$LOGS/smoke.log" | tail -1)
    [ -n "$result" ] || fail "smoke test printed no RESULT line (exit $code)"
    [ "$code" -eq 0 ] && [[ "$result" == *PASS* ]] || fail "$result"
    check_log "$LOGS/smoke.log"
    step "smoke: $result"
}

run_build() {
    step "standalone build (current OS)"
    unity -executeMethod BusDriver.Editor.Build.BuildScripts.BuildCurrent -logFile "$LOGS/build.log"
    local code=$?
    local line
    line=$(grep -E '^\[BUILD\] (OK|FAIL)' "$LOGS/build.log" | tail -1)
    [ "$code" -eq 0 ] && [[ "$line" == *"[BUILD] OK"* ]] || fail "${line:-build exited $code with no [BUILD] line}"
    check_log "$LOGS/build.log"
    local version exe
    version=$(sed -n 's/^  bundleVersion: //p' "$PROJECT/ProjectSettings/ProjectSettings.asset" | head -1)
    case "$(uname -s)" in
        Darwin)
            # The executable inside the bundle is named after the product, which has a space
            local app="$ROOT/Builds/mac/$version/BusDriver.app"
            local name
            name=$(/usr/libexec/PlistBuddy -c "Print :CFBundleExecutable" "$app/Contents/Info.plist" 2>/dev/null)
            exe="$app/Contents/MacOS/${name:-BusDriver}"
            ;;
        *) exe="$ROOT/Builds/windows/$version/BusDriver.exe" ;;
    esac
    [ -x "$exe" ] || fail "no player at $exe"
    step "selftest: $exe"
    # Headless, so a verify run never opens a window on the desktop
    "$exe" -batchmode -nographics --selftest -logFile "$LOGS/selftest.log" > /dev/null 2>&1
    code=$?
    local ok
    ok=$(grep '\[SELFTEST\] OK' "$LOGS/selftest.log" | tail -1)
    [ "$code" -eq 0 ] && [ -n "$ok" ] || fail "selftest exit $code: $(grep '\[SELFTEST\]' "$LOGS/selftest.log" | tail -1)"
    step "build: ${ok#*\] }"
}

MODE="${1:-}"
case "$MODE" in
    quick) run_compile; run_editmode ;;
    content) run_content ;;
    playmode) run_playmode ;;
    smoke) run_smoke ;;
    build) run_build ;;
    full) run_content; run_playmode; run_smoke; run_build ;;
    *)
        echo "usage: tools/verify.sh quick|content|playmode|smoke|build|full" >&2
        exit 64
        ;;
esac
echo "verify: PASS ($MODE)"
