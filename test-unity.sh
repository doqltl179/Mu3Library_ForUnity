#!/usr/bin/env bash

# Runs the EditMode or PlayMode tests of the development projects through the
# Unity Test Framework CLI. Target selection, the Editor lookup, the lock check,
# the sample links, and the isolated mirror come from compile-unity.sh, which
# this sources.

set -euo pipefail

# shellcheck source=compile-unity.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/compile-unity.sh"

SELECTION_LABEL="test"
readonly RESULTS_DIR="$REPO_ROOT/log/unity-tests"
# The Unity Test Framework platform as --platform names it and as Unity does.
PLATFORM_KEY="editmode"
TEST_PLATFORM="EditMode"
# The pattern --filter passes to Unity's -testFilter; empty runs every test.
TEST_FILTER=""
# How long a target's Unity log may stay the same before compile-unity.sh's
# run_unity_for_target stops Unity as stalled; --stall-timeout sets it.
UNITY_STALL_SECONDS=600
# The arguments left for compile-unity.sh's parse_arguments once this script's own options are out.
COMPILE_ARGUMENTS=()
SUMMARY_LINES=()
FAILED_TARGET_COUNT=0

usage() {
    local i

    cat <<'EOF'
Usage: ./test-unity.sh [changed|all|target] [options]

Runs the EditMode or PlayMode tests of the selected development projects with
the Unity Test Framework, and exits non-zero when a test fails or a run reports
no result.

Selection:
  changed          Test every project that consumes a package affected by Git
                   changes (default), selected as compile-unity.sh selects it.
  all              Test every configured project.
EOF

    for ((i = 0; i < ${#TARGET_KEYS[@]}; i++)); do
        printf '  %-16s Test %s\n' "${TARGET_KEYS[$i]}" "${PROJECT_PATHS[$i]}"
    done

    cat <<'EOF'

Options:
  --platform <editmode|playmode>
                    Run the EditMode tests (default) or the PlayMode tests.
  --filter <pattern>
                    Run only the tests whose full name matches <pattern>,
                    the regular expression Unity's -testFilter takes, such
                    as a fixture's full name; separate several with ";".
                    The [Explicit] tests it matches run too. It may not
                    start with - or /.
  --stall-timeout <seconds>
                    Stop Unity and fail the target once its log has not
                    grown for this long, naming the test that was running
                    (default 600; 0 waits as long as Unity runs).
  --base <git-ref>  Include committed changes from <git-ref>...HEAD.
  --dry-run         Print the selection and where each target would run,
                    without Unity.
  --isolated        Always test a temporary source-only project mirror.
  --in-place        Test the repository project directly; fail if it is open.
  --keep-staging    Keep an isolated project after the run for diagnostics.
  --without-samples Run even when a project's link to its package samples is
                    missing, leaving the samples out with a warning.
  --editor-version <version>
                    Test with the Unity Hub Editor of that exact version, such
                    as 6000.3.17f1, instead of the one ProjectVersion.txt
                    names, always in an isolated mirror retargeted to it.
  -h, --help        Show this help.

A project runs the tests of the packages its Packages/manifest.json lists under
"testables"; a selected project that lists none is skipped. Each run writes
NUnit XML results to log/unity-tests/<target>-<platform>.xml, such as
log/unity-tests/urp-playmode.xml, and Unity's log, with a line as each test
begins and ends, beside it as <target>-<platform>.log. A failed test or a
stalled or failed Unity run does not stop the remaining targets, and a summary
closes the run; a target that cannot start, such as an open project under
--in-place or a mirror that cannot be copied, stops the script at once.

Where a target runs (in place, or in an isolated mirror while an Editor has the
project open), how --editor-version retargets the mirror, the sample-link
check, and the environment variables work as in compile-unity.sh; see
./compile-unity.sh --help.
EOF
}

# Takes --platform, --filter, and --stall-timeout, which only this script has,
# out of the arguments and leaves the rest in COMPILE_ARGUMENTS for the options
# it shares with compile-unity.sh.
parse_test_arguments() {
    COMPILE_ARGUMENTS=()

    while [[ "$#" -gt 0 ]]; do
        case "$1" in
            --platform)
                shift
                [[ "$#" -gt 0 ]] || fail "--platform requires editmode or playmode"
                case "$1" in
                    editmode)
                        TEST_PLATFORM="EditMode"
                        ;;
                    playmode)
                        TEST_PLATFORM="PlayMode"
                        ;;
                    *)
                        fail "--platform expects editmode or playmode: $1"
                        ;;
                esac
                PLATFORM_KEY="$1"
                ;;
            --filter)
                shift
                [[ "$#" -gt 0 && "$1" =~ [^[:space:]] ]] || fail "--filter requires a test name or pattern"
                [[ "$1" != -* ]] || fail "--filter expects a test name or pattern, not an option: $1"
                [[ "$1" != /* ]] || fail "--filter cannot start with /, which Git Bash rewrites as a file path: $1"
                [[ -z "$TEST_FILTER" ]] || fail "--filter can be given once; separate several patterns with ;"
                TEST_FILTER="$1"
                ;;
            --stall-timeout)
                shift
                [[ "$#" -gt 0 && "$1" =~ ^[0-9]+$ ]] || fail "--stall-timeout expects a whole number of seconds"
                UNITY_STALL_SECONDS="$((10#$1))"
                ;;
            *)
                COMPILE_ARGUMENTS+=("$1")
                ;;
        esac
        shift
    done
}

# Unity runs package tests only for the packages a project's manifest lists
# under "testables", so a project without that list has nothing to run.
project_has_testables() {
    local target_key="$1"
    local project_path

    project_path="$(project_path_for_target "$target_key")" || return 1
    grep -q '"testables"' "$REPO_ROOT/$project_path/Packages/manifest.json" 2>/dev/null
}

no_testables_reason() {
    printf '%s/Packages/manifest.json lists no testables' "$(project_path_for_target "$1")"
}

run_plan_entry() {
    local target_key="$1"

    if ! project_has_testables "$target_key"; then
        printf 'skipped, %s\n' "$(no_testables_reason "$target_key")"
        return 0
    fi

    run_location_phrase "$target_key"
}

# Narrows SELECTED_TARGETS to the projects that have tests to run and records
# the others as skipped in the summary.
keep_testable_targets() {
    local target_key
    local testable_targets=()

    [[ "${#SELECTED_TARGETS[@]}" -gt 0 ]] || return 0

    for target_key in "${SELECTED_TARGETS[@]}"; do
        if project_has_testables "$target_key"; then
            testable_targets+=("$target_key")
        else
            SUMMARY_LINES+=("$target_key: skipped, $(no_testables_reason "$target_key")")
        fi
    done

    SELECTED_TARGETS=()
    if [[ "${#testable_targets[@]}" -gt 0 ]]; then
        SELECTED_TARGETS=("${testable_targets[@]}")
    fi
}

# Prints a numeric attribute of the <test-run> element that opens an NUnit 3
# result file.
test_run_attribute() {
    local results_file="$1"
    local name="$2"
    local header
    local pattern="[[:space:]]$name=\"([0-9]+)\""

    header="$(grep -m 1 -o '<test-run [^>]*>' "$results_file")" || return 1
    [[ "$header" =~ $pattern ]] || return 1
    printf '%s\n' "${BASH_REMATCH[1]}"
}

print_failed_tests() {
    local results_file="$1"
    local test_case
    local pattern='fullname="([^"]*)"'

    while IFS= read -r test_case; do
        if [[ "$test_case" =~ $pattern ]]; then
            printf '  failed: %s\n' "${BASH_REMATCH[1]}" >&2
        fi
    done < <(grep -o '<test-case [^>]*result="Failed"[^>]*>' "$results_file" || true)
}

# Prints the full name of the test that Unity began last and did not end, from
# the lines that -automated adds to its log, or nothing.
unfinished_test() {
    local log_file="$1"
    local status_line
    # A JSON string, whose escaped quotes, as in a test case's string argument,
    # do not end it.
    local pattern='"name":"((\\.|[^"\\])*)"'

    status_line="$(grep -o '##utp:{"type":"TestStatus".*' "$log_file" 2>/dev/null | tail -n 1 || true)"
    if [[ "$status_line" == *'"phase":"Begin"'* && "$status_line" =~ $pattern ]]; then
        printf '%s\n' "${BASH_REMATCH[1]}" | sed -e 's/\\"/"/g' -e 's/\\\\/\\/g'
    fi
}

record_failure() {
    local target_key="$1"
    local reason="$2"

    printf 'error: %s: %s\n' "$target_key" "$reason" >&2
    SUMMARY_LINES+=("$target_key: FAILED, $reason")
    FAILED_TARGET_COUNT=$((FAILED_TARGET_COUNT + 1))
}

test_target() {
    local target_key="$1"
    local results_file="$RESULTS_DIR/$target_key-$PLATFORM_KEY.xml"
    local total
    local passed
    local failed
    local skipped
    local counts
    local unfinished
    local stall_phrase
    local unity_arguments=(-runTests -testPlatform "$TEST_PLATFORM")

    if [[ -n "$TEST_FILTER" ]]; then
        unity_arguments+=(-testFilter "$TEST_FILTER")
    fi

    mkdir -p "$RESULTS_DIR"
    # A result file left by an earlier run must not stand in for this one.
    rm -f -- "$results_file"
    UNITY_LOG_FILE="$RESULTS_DIR/$target_key-$PLATFORM_KEY.log"

    # -automated makes the Unity Test Framework log a line as each test begins
    # and ends, which names the test that a stalled or crashed run was in.
    # Git Bash rewrites an argument that looks like a file path, such as one
    # holding "=/", before a Windows program sees it; MSYS2_ARG_CONV_EXCL
    # names the filter by its first ";" part, so Unity gets it unchanged.
    MSYS2_ARG_CONV_EXCL="${MSYS2_ARG_CONV_EXCL:+$MSYS2_ARG_CONV_EXCL;}${TEST_FILTER%%;*}" \
        run_unity_for_target "$target_key" "Testing" \
        "${unity_arguments[@]}" \
        -testResults "$(to_windows_path "$results_file")" \
        -automated
    unfinished="$(unfinished_test "$UNITY_LOG_FILE")"

    if [[ "$UNITY_STALLED" -eq 1 ]]; then
        if [[ -n "$unfinished" ]]; then
            stall_phrase="while running $unfinished"
        else
            stall_phrase="while no test was running"
        fi
        record_failure "$target_key" "Unity wrote nothing to its log for $UNITY_STALL_SECONDS s $stall_phrase, so the script stopped it; see ${UNITY_LOG_FILE#"$REPO_ROOT"/}"
        return 0
    fi

    if [[ ! -f "$results_file" ]]; then
        record_failure "$target_key" "Unity exited with $UNITY_STATUS and wrote no test results${unfinished:+ while running $unfinished}; see its log above"
        return 0
    fi

    if ! total="$(test_run_attribute "$results_file" total)" \
        || ! passed="$(test_run_attribute "$results_file" passed)" \
        || ! failed="$(test_run_attribute "$results_file" failed)" \
        || ! skipped="$(test_run_attribute "$results_file" skipped)"; then
        record_failure "$target_key" "could not read the test counts in ${results_file#"$REPO_ROOT"/}"
        return 0
    fi
    counts="$total tests, $passed passed, $failed failed, $skipped skipped"

    if [[ "$failed" -gt 0 ]]; then
        record_failure "$target_key" "$counts"
        print_failed_tests "$results_file"
    elif [[ "$total" -eq 0 && -n "$TEST_FILTER" ]]; then
        record_failure "$target_key" "no test matched --filter $TEST_FILTER"
    elif [[ "$total" -eq 0 ]]; then
        record_failure "$target_key" "no tests ran although the project lists testables"
    elif [[ "$UNITY_STATUS" -ne 0 ]]; then
        record_failure "$target_key" "Unity exited with $UNITY_STATUS although no test failed ($counts)"
    else
        printf '%s tests passed for %s: %s\n' "$TEST_PLATFORM" "$target_key" "$counts"
        SUMMARY_LINES+=("$target_key: $counts")
    fi
}

print_summary() {
    local line

    printf '%s test summary:\n' "$TEST_PLATFORM"
    if [[ "${#SUMMARY_LINES[@]}" -gt 0 ]]; then
        for line in "${SUMMARY_LINES[@]}"; do
            printf '  - %s\n' "$line"
        done
    fi
}

test_main() {
    local target_key

    load_package_config
    parse_test_arguments "$@"
    parse_arguments ${COMPILE_ARGUMENTS[@]+"${COMPILE_ARGUMENTS[@]}"}
    select_requested_targets
    print_selection
    if [[ -n "$TEST_FILTER" ]]; then
        printf 'Test filter: %s\n' "$TEST_FILTER"
    fi

    if [[ "$DRY_RUN" -eq 1 ]]; then
        print_run_plan
        keep_testable_targets
        check_sample_links warning || true
        exit 0
    fi

    if [[ "${#SELECTED_TARGETS[@]}" -eq 0 ]]; then
        printf 'No Unity package or development-project changes detected; nothing to test.\n'
        exit 0
    fi

    keep_testable_targets
    if [[ "${#SELECTED_TARGETS[@]}" -eq 0 ]]; then
        print_summary
        exit 0
    fi

    trap cleanup_staging EXIT

    preflight_selected_targets || fail "preflight failed; no tests were run"

    for target_key in "${SELECTED_TARGETS[@]}"; do
        test_target "$target_key"
    done

    print_summary
    printf 'Results: log/unity-tests/<target>-%s.xml, with Unity logs as <target>-%s.log\n' "$PLATFORM_KEY" "$PLATFORM_KEY"

    if [[ "$FAILED_TARGET_COUNT" -gt 0 ]]; then
        fail "$TEST_PLATFORM tests failed for $FAILED_TARGET_COUNT target(s)"
    fi
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    test_main "$@"
fi
