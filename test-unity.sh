#!/usr/bin/env bash

# Runs the EditMode tests of the development projects through the Unity Test
# Framework CLI. Target selection, the Editor lookup, the lock check, the sample
# links, and the isolated mirror come from compile-unity.sh, which this sources.

set -euo pipefail

# shellcheck source=compile-unity.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/compile-unity.sh"

SELECTION_LABEL="test"
readonly RESULTS_DIR="$REPO_ROOT/log/unity-tests"
SUMMARY_LINES=()
FAILED_TARGET_COUNT=0

usage() {
    local i

    cat <<'EOF'
Usage: ./test-unity.sh [changed|all|target] [options]

Runs the EditMode tests of the selected development projects with the Unity
Test Framework, and exits non-zero when a test fails or a run reports no result.

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
  --base <git-ref>  Include committed changes from <git-ref>...HEAD.
  --dry-run         Print the selection and where each target would run,
                    without Unity.
  --isolated        Always test a temporary source-only project mirror.
  --in-place        Test the repository project directly; fail if it is open.
  --keep-staging    Keep an isolated project after the run for diagnostics.
  --without-samples Run even when a project's link to its package samples is
                    missing, leaving the samples out with a warning.
  -h, --help        Show this help.

A project runs the tests of the packages its Packages/manifest.json lists under
"testables"; a selected project that lists none is skipped. Each run writes
NUnit XML results to log/unity-tests/<target>-editmode.xml. Every selected
target runs even after an earlier one fails, and a summary closes the run.

Where a target runs (in place, or in an isolated mirror while an Editor has the
project open), the sample-link check, and the environment variables work as in
compile-unity.sh; see ./compile-unity.sh --help.
EOF
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

record_failure() {
    local target_key="$1"
    local reason="$2"

    printf 'error: %s: %s\n' "$target_key" "$reason" >&2
    SUMMARY_LINES+=("$target_key: FAILED, $reason")
    FAILED_TARGET_COUNT=$((FAILED_TARGET_COUNT + 1))
}

test_target() {
    local target_key="$1"
    local results_file="$RESULTS_DIR/$target_key-editmode.xml"
    local total
    local passed
    local failed
    local skipped
    local counts

    mkdir -p "$RESULTS_DIR"
    # A result file left by an earlier run must not stand in for this one.
    rm -f -- "$results_file"

    run_unity_for_target "$target_key" "Testing" \
        -runTests \
        -testPlatform EditMode \
        -testResults "$(to_windows_path "$results_file")"

    if [[ ! -f "$results_file" ]]; then
        record_failure "$target_key" "Unity exited with $UNITY_STATUS and wrote no test results; see its log above"
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
    elif [[ "$total" -eq 0 ]]; then
        record_failure "$target_key" "no tests ran although the project lists testables"
    elif [[ "$UNITY_STATUS" -ne 0 ]]; then
        record_failure "$target_key" "Unity exited with $UNITY_STATUS although no test failed ($counts)"
    else
        printf 'EditMode tests passed for %s: %s\n' "$target_key" "$counts"
        SUMMARY_LINES+=("$target_key: $counts")
    fi
}

print_summary() {
    local line

    printf 'EditMode test summary:\n'
    if [[ "${#SUMMARY_LINES[@]}" -gt 0 ]]; then
        for line in "${SUMMARY_LINES[@]}"; do
            printf '  - %s\n' "$line"
        done
    fi
}

test_main() {
    local target_key

    load_package_config
    parse_arguments "$@"
    select_requested_targets
    print_selection

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
    printf 'Results: log/unity-tests/<target>-editmode.xml\n'

    if [[ "$FAILED_TARGET_COUNT" -gt 0 ]]; then
        fail "EditMode tests failed for $FAILED_TARGET_COUNT target(s)"
    fi
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    test_main "$@"
fi
