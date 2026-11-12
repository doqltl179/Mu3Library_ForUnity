#!/usr/bin/env bash

# Runs the EditMode or PlayMode tests of the development projects through the
# Unity Test Framework CLI. Target selection, the Editor lookup, the lock check,
# the sample links, and the isolated mirror come from compile-unity.sh, which
# this sources.

set -euo pipefail

# compile-unity.sh lies beside this file, whose folder is cut from its path as
# compile-unity.sh cuts its own.
case "${OSTYPE:-}" in
    msys*|cygwin*) COMPILE_SCRIPT_DIR="${BASH_SOURCE[0]%[/\\]*}" ;;
    *) COMPILE_SCRIPT_DIR="${BASH_SOURCE[0]%/*}" ;;
esac
if [[ "$COMPILE_SCRIPT_DIR" == "${BASH_SOURCE[0]}" ]]; then
    COMPILE_SCRIPT_DIR="."
fi
# shellcheck source=compile-unity.sh
source "$COMPILE_SCRIPT_DIR/compile-unity.sh"

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
# How many runs test_target failed whose one failure came from the
# environment, which the closing error line names.
ENVIRONMENT_FAILED_COUNT=0
# Set by project_has_testables, find_unfinished_test, find_data_store_loss,
# and find_tests_with_result.
NO_TESTABLES_REASON=""
UNFINISHED_TEST=""
DATA_STORE_LOSS_PID=""
DATA_STORE_LOSS_TEST=""
DATA_STORE_LOSS_ERROR=""
RESULT_TESTS=()
# When the Data Store service that find_data_store_loss named ended and with
# what exit code, as read_data_store_end reads them from the helper's record.
DATA_STORE_ENDED=""
DATA_STORE_EXIT_CODE=""

# How many spinners --load-spinners asks for, each a Python process that spins
# a CPU core from the first test of a run until its last test ends, and how many
# times --repeat runs each target.
LOAD_SPINNERS=0
REPEAT_COUNT=1
# The helper that starts the spinners beside each run and counts the Unity
# processes that are not the run's own, the Python that runs it, found by
# find_load_python, or why there is none. The tool tests set
# UNITY_LOAD_SPINNERS_IDLE=1 so that their spinners wait instead of spinning.
readonly LOAD_HELPER="$REPO_ROOT/tools/cli/src/mu3_cli/fixed_load.py"
LOAD_PYTHON=""
LOAD_UNAVAILABLE=""
LOAD_HELPER_PID=""
LOAD_STOP_FILE=""
# What the helper recorded for the run that test_target ran last, which
# read_load_record reads.
LOAD_STATE=""
LOAD_CPUS=""
LOAD_ENDED_BEFORE=""
LOAD_SECONDS=""
LOAD_LAG_MS=""
LOAD_ENDED=""
LOAD_ERROR=""
OTHERS_AT_START=""
OTHERS_MIN=""
OTHERS_MAX=""
OTHERS_LOOKS=""
COUNT_ERROR=""
LOAD_PROBLEM=""
# How the summary and the file names tell a run of --repeat from the others,
# such as " run 2 of 3" and "-run2", and the counts test_target read for it.
RUN_LABEL=""
RUN_FILE_SUFFIX=""
RUN_COUNTS=()
# The totals of the runs of one target under --repeat: the runs that failed and
# that wrote counts, the sums of total, passed, failed, skipped, and
# inconclusive, each failed or inconclusive test with the number of runs it
# had that result in, and the fewest and most other Unity processes counted.
REPEAT_FAILED_RUNS=0
REPEAT_COUNTED_RUNS=0
REPEAT_SUMS=()
REPEAT_TEST_RESULTS=()
REPEAT_TEST_NAMES=()
REPEAT_TEST_RUNS=()
REPEAT_OTHERS_LOW=""
REPEAT_OTHERS_HIGH=""
# The runs of one target under --repeat in which Unity's Data Store service
# ended, and those of them whose one failure came from it.
REPEAT_DATA_STORE_LOSSES=0
REPEAT_ENVIRONMENT_FAILED_RUNS=0

usage() {
    local i

    cat <<'EOF'
Usage: ./test-unity.sh [changed|all|target] [options]

Runs the EditMode or PlayMode tests of the selected development projects with
the Unity Test Framework, and exits non-zero when a test fails or is
inconclusive or a run reports no result.

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
  --load-spinners <n>
                    Start <n> Python processes that each spin a CPU core as
                    the first test of each run begins, and stop them as its
                    last planned test ends, or else when its Unity exits, so
                    that timings are measured under a fixed load that slows
                    neither the compile before nor the shutdown after
                    (default 0, none). A run under which no test ran with
                    the load fails.
  --repeat <n>      Run each selected target <n> times, starting Unity anew
                    for each run, keep each run's results apart, and total
                    them (default 1).
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

Under each run the summary gives how many other Unity processes, those the run
did not start, ran as it started and while it ran, and with --load-spinners
when the load started and how long it lasted. A helper counts them and runs the
spinners with python or python3 of version 3.10 or later from PATH; without
one, the processes go uncounted and --load-spinners stops the script before
Unity starts. Each run keeps that record beside its results as
<target>-<platform>.load. With --repeat, run <k> writes
<target>-<platform>-run<k>.xml, .log, .load, and .uds-service.log instead,
and the summary closes each target with the totals of its runs and, for each
failed or inconclusive test, the number of runs it had that result in.

When Unity's Data Store service, the process that serves the Editor the files
it imported, ends before the Editor does, Unity logs an error that fails the
test then running, whatever that test checks. The summary then names the
service's process ID and that test under the run. When the test's failure
message holds that error and nothing else, the summary says so, and a run
whose one failure that was says that the failure came from the environment.
It still fails, since that test reached no verdict: run the target again.
The helper records in the .load record when each Data Store service of the
run started and ended and its exit code, and a run whose Unity launched the
service keeps the log the service wrote, the project's Logs/UDS-Service.log,
as <target>-<platform>.uds-service.log. Under a run in which the service
ended, the summary gives its exit code, when it ended, and that log.

Where a target runs (in place, or in an isolated mirror while an Editor has the
project open), how --editor-version retargets the mirror, the sample-link
check, the warnings about the Windows path limit, which the summary gives again
under each target, and the environment variables work as in compile-unity.sh;
see ./compile-unity.sh --help.
EOF
}

# Takes --platform and --filter, which only this script has, out of the
# arguments and leaves the rest in COMPILE_ARGUMENTS for the options it shares
# with compile-unity.sh.
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
            --load-spinners)
                shift
                [[ "$#" -gt 0 && "$1" =~ ^[0-9]+$ ]] || fail "--load-spinners expects a whole number of spinners"
                LOAD_SPINNERS="$((10#$1))"
                ;;
            --repeat)
                shift
                [[ "$#" -gt 0 && "$1" =~ ^[0-9]+$ && "$((10#$1))" -ge 1 ]] \
                    || fail "--repeat expects a whole number of runs, 1 or more"
                REPEAT_COUNT="$((10#$1))"
                ;;
            *)
                COMPILE_ARGUMENTS+=("$1")
                ;;
        esac
        shift
    done
}

# Unity runs package tests only for the packages a project's manifest lists
# under "testables", so a project without that list has nothing to run; this
# returns 1 for it, with the reason in NO_TESTABLES_REASON.
project_has_testables() {
    local target_key="$1"
    local manifest_path
    local line

    NO_TESTABLES_REASON=""
    find_target "$target_key" || return 1
    manifest_path="${PROJECT_PATHS[$TARGET_INDEX]}/Packages/manifest.json"
    NO_TESTABLES_REASON="$manifest_path lists no testables"

    [[ -f "$REPO_ROOT/$manifest_path" && -r "$REPO_ROOT/$manifest_path" ]] || return 1
    # As in compile-unity.sh, a line read from a CRLF checkout drops its CR.
    while IFS= read -r line || [[ -n "$line" ]]; do
        line="${line%$'\r'}"
        if [[ "$line" == *'"testables"'* ]]; then
            return 0
        fi
    done < "$REPO_ROOT/$manifest_path"

    return 1
}

run_plan_entry() {
    local target_key="$1"

    if ! project_has_testables "$target_key"; then
        printf 'skipped, %s\n' "$NO_TESTABLES_REASON"
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
            SUMMARY_LINES+=("$target_key: skipped, $NO_TESTABLES_REASON")
        fi
    done

    SELECTED_TARGETS=()
    if [[ "${#testable_targets[@]}" -gt 0 ]]; then
        SELECTED_TARGETS=("${testable_targets[@]}")
    fi
}

# Reads the counts of the <test-run> element that opens an NUnit 3 result file
# into the variables total, passed, failed, skipped, and inconclusive, which
# test_target declares, or returns 1 when the element or one of the counts is
# missing.
read_test_run_counts() {
    local results_file="$1"
    local line
    local header=""
    local count_name
    local element_pattern='<test-run [^>]*>'
    local count_pattern

    while IFS= read -r line || [[ -n "$line" ]]; do
        line="${line%$'\r'}"
        if [[ "$line" =~ $element_pattern ]]; then
            header="${BASH_REMATCH[0]}"
            break
        fi
    done < "$results_file"

    for count_name in total passed failed skipped inconclusive; do
        count_pattern="[[:space:]]$count_name=\"([0-9]+)\""
        [[ "$header" =~ $count_pattern ]] || return 1
        printf -v "$count_name" '%s' "${BASH_REMATCH[1]}"
    done
}

# Sets XML_TEXT to an XML attribute value with its references replaced by the
# characters they stand for: the entities XML predefines, such as the &quot;
# of a test case that takes a string, and the numeric character references,
# whose characters it writes in UTF-8 whatever the locale. It reads the value
# from left to right, so that the text an &amp; leaves is not read again, and
# builds the text without ${var//pattern/replacement}, whose replacement
# bash 5.2 reads an unquoted & in. A reference it does not know stays as it
# is, as does one to a character that a bash string cannot hold or that is not
# a character, such as &#0; or a surrogate.
xml_attribute_text() {
    local text="$1"
    local pattern='^(quot|apos|lt|gt|amp|#[0-9]{1,7}|#x[0-9A-Fa-f]{1,6});'
    local reference
    local code
    local bytes

    XML_TEXT=""
    while [[ "$text" == *'&'* ]]; do
        XML_TEXT+="${text%%&*}"
        text="${text#*&}"
        if [[ ! "$text" =~ $pattern ]]; then
            XML_TEXT+='&'
            continue
        fi
        reference="${BASH_REMATCH[1]}"
        case "$reference" in
            quot) XML_TEXT+='"' ;;
            apos) XML_TEXT+="'" ;;
            lt) XML_TEXT+='<' ;;
            gt) XML_TEXT+='>' ;;
            amp) XML_TEXT+='&' ;;
            *)
                if [[ "$reference" == '#x'* ]]; then
                    code=$((16#${reference#'#x'}))
                else
                    code=$((10#${reference#'#'}))
                fi
                if ((code == 0 || (code >= 0xD800 && code <= 0xDFFF) || code > 0x10FFFF)); then
                    XML_TEXT+='&'
                    continue
                fi
                # Octal escapes in a printf format write bytes, not characters
                # of the locale.
                if ((code < 0x80)); then
                    printf -v bytes '\\%03o' "$code"
                elif ((code < 0x800)); then
                    printf -v bytes '\\%03o\\%03o' $((0xC0 | code >> 6)) $((0x80 | (code & 0x3F)))
                elif ((code < 0x10000)); then
                    printf -v bytes '\\%03o\\%03o\\%03o' $((0xE0 | code >> 12)) \
                        $((0x80 | (code >> 6 & 0x3F))) $((0x80 | (code & 0x3F)))
                else
                    printf -v bytes '\\%03o\\%03o\\%03o\\%03o' $((0xF0 | code >> 18)) \
                        $((0x80 | (code >> 12 & 0x3F))) $((0x80 | (code >> 6 & 0x3F))) $((0x80 | (code & 0x3F)))
                fi
                # shellcheck disable=SC2059
                printf -v bytes "$bytes"
                XML_TEXT+="$bytes"
                ;;
        esac
        text="${text#"$reference";}"
    done
    XML_TEXT+="$text"
}

# Sets RESULT_TESTS to the full names of the test cases in an NUnit 3 result
# file whose result is the one given, such as Failed or Inconclusive, as the
# tests have them, with the XML references of the result file replaced; a
# suite whose tests all had that result is not one of them.
find_tests_with_result() {
    local results_file="$1"
    local result="$2"
    local test_case
    local pattern='fullname="([^"]*)"'

    RESULT_TESTS=()
    while IFS= read -r test_case; do
        if [[ "$test_case" =~ $pattern ]]; then
            xml_attribute_text "${BASH_REMATCH[1]}"
            RESULT_TESTS+=("$XML_TEXT")
        fi
    done < <(grep -o "<test-case [^>]*result=\"$result\"[^>]*>" "$results_file" || true)
}

# Names the failed and the inconclusive tests of the target that record_failure
# has just recorded, and adds the inconclusive ones to its summary entry, since
# its counts alone do not say which tests reached no verdict.
name_failed_and_inconclusive_tests() {
    local results_file="$1"
    local test_name

    find_tests_with_result "$results_file" Failed
    for test_name in ${RESULT_TESTS[@]+"${RESULT_TESTS[@]}"}; do
        printf '  failed: %s\n' "$test_name" >&2
    done

    find_tests_with_result "$results_file" Inconclusive
    for test_name in ${RESULT_TESTS[@]+"${RESULT_TESTS[@]}"}; do
        printf '  inconclusive: %s\n' "$test_name" >&2
        SUMMARY_LINES+=("  inconclusive: $test_name")
    done
}

# Sets UNFINISHED_TEST to the full name of the test that Unity began last and
# did not end, from the lines that -automated adds to its log, or to nothing.
find_unfinished_test() {
    local log_file="$1"
    local status_line
    # A JSON string, whose escaped quotes, as in a test case's string argument,
    # do not end it.
    local pattern='"name":"((\\.|[^"\\])*)"'

    UNFINISHED_TEST=""
    status_line="$(grep -o '##utp:{"type":"TestStatus".*' "$log_file" 2>/dev/null | tail -n 1 || true)"
    if [[ "$status_line" == *'"phase":"Begin"'* && "$status_line" =~ $pattern ]]; then
        UNFINISHED_TEST="$(printf '%s\n' "${BASH_REMATCH[1]}" | sed -e 's/\\"/"/g' -e 's/\\\\/\\/g')"
    fi
}

# Prints the test that find_unfinished_test finds, or nothing.
unfinished_test() {
    find_unfinished_test "$1"
    if [[ -n "$UNFINISHED_TEST" ]]; then
        printf '%s\n' "$UNFINISHED_TEST"
    fi
}

# Sets DATA_STORE_LOSS_PID to the process ID that Unity's log names once the
# Unity Data Store service, the process that serves the Editor the files it
# imported, has ended before the Editor, DATA_STORE_LOSS_TEST to the full name
# of the test that had begun and not ended then, and DATA_STORE_LOSS_ERROR to
# the error as Unity logged it, or all three to nothing. Unity logs that end as
# an error once, when it next reads such a file, and reads them in its own
# process from then on. The line that ends a test is passed over, since its
# failure message can quote the error.
find_data_store_loss() {
    local log_file="$1"

    DATA_STORE_LOSS_PID=""
    DATA_STORE_LOSS_TEST=""
    DATA_STORE_LOSS_ERROR=""
    [[ -f "$log_file" ]] || return 0

    {
        IFS= read -r DATA_STORE_LOSS_PID || true
        IFS= read -r DATA_STORE_LOSS_TEST || true
        IFS= read -r DATA_STORE_LOSS_ERROR || true
    } < <(awk '
        BEGIN {
            status = "##utp:{\"type\":\"TestStatus\""
            name_key = "\"name\":\""
            marker = "Unity Data Store service (pid "
            marker_end = ") is no longer running."
            running = ""
        }
        { sub(/\r$/, "") }
        index($0, status) == 1 {
            running = ""
            start = index($0, name_key)
            if (index($0, "\"phase\":\"Begin\"") == 0 || start == 0) {
                next
            }
            # The name is a JSON string, whose escaped quotes, as in a test
            # case string argument, do not end it. A \uXXXX escape is not
            # decoded, so such a name matches no test of the result file, and
            # the end of the service is then only a warning.
            rest = substr($0, start + length(name_key))
            for (i = 1; i <= length(rest); i++) {
                c = substr(rest, i, 1)
                if (c == "\\") {
                    i++
                    c = substr(rest, i, 1)
                } else if (c == "\"") {
                    break
                }
                running = running c
            }
            next
        }
        {
            start = index($0, marker)
            if (start == 0) {
                next
            }
            rest = substr($0, start + length(marker))
            end = index(rest, marker_end)
            pid = substr(rest, 1, end - 1)
            if (end == 0 || pid !~ /^[0-9]+$/) {
                next
            }
            # Unity logs the error as a line of its own before the LogEntry
            # line that quotes it, so the rest of the line is the error; in a
            # LogEntry line it ends where its JSON string does, since it holds
            # no quote.
            error = substr($0, start)
            if (index($0, "##utp:") == 1) {
                error = substr(error, 1, index(error, "\"") - 1)
            }
            print pid
            print running
            print error
            exit
        }
    ' "$log_file")
}

# Returns 0 when the test that find_data_store_loss named failed in a result
# file on nothing but the error that find_data_store_loss read, as its failure
# message shows in one of the two forms such a failure takes, each perhaps
# after "TearDown : ". The Unity Test Framework writes
#   Unhandled log message: '[Error] <error>'. Use UnityEngine.TestTools.LogAssert.Expect
# and the TearDown of Mu3WindowTests writes
#   Unhandled log message: [Error] <error>
#   Stack trace of the first:
#   <the stack trace Unity logged with the error, which ends in a line break>
# A message that holds anything else, such as an assertion that failed, another
# log after " | ", or a failure that the TearDown adds after the stack trace,
# shows a failure of the test's own, as does a test that lets failing logs pass
# with LogAssert.ignoreFailingMessages and failed while the error went by.
failed_only_on_data_store_loss() {
    local results_file="$1"

    [[ -n "$DATA_STORE_LOSS_TEST" && -n "$DATA_STORE_LOSS_ERROR" && -f "$results_file" ]] || return 1

    # The values reach awk through the environment, since -v would read the
    # backslashes of a test name as escapes.
    DATA_STORE_LOSS_TEST="$DATA_STORE_LOSS_TEST" DATA_STORE_LOSS_ERROR="$DATA_STORE_LOSS_ERROR" awk '
        # An attribute value with the entities XML predefines replaced. A
        # character reference such as &#xA; stays, so such a name matches no
        # test, and the end of the service is then only a warning.
        function xml_text(text) {
            gsub(/&quot;/, "\"", text)
            gsub(/&apos;/, "\047", text)
            gsub(/&lt;/, "<", text)
            gsub(/&gt;/, ">", text)
            gsub(/&amp;/, "\\&", text)
            return text
        }
        function attribute(line, key,    start, rest) {
            start = index(line, " " key "=\"")
            if (start == 0) {
                return ""
            }
            rest = substr(line, start + length(key) + 3)
            return substr(rest, 1, index(rest, "\"") - 1)
        }
        BEGIN {
            name = ENVIRON["DATA_STORE_LOSS_TEST"]
            error = ENVIRON["DATA_STORE_LOSS_ERROR"]
            opening = "<message><![CDATA["
            closing = "]]></message>"
            state = 0
            count = 0
            found = 0
        }
        { sub(/\r$/, "") }
        state == 0 {
            if (index($0, "<test-case ") == 0 || attribute($0, "result") != "Failed" \
                || xml_text(attribute($0, "fullname")) != name) {
                next
            }
            if ($0 ~ /\/>[[:space:]]*$/) {
                exit
            }
            state = 1
            next
        }
        state == 1 {
            if (index($0, "</test-case>")) {
                exit
            }
            start = index($0, opening)
            if (start == 0) {
                next
            }
            $0 = substr($0, start + length(opening))
            state = 2
        }
        state == 2 {
            end = index($0, closing)
            if (end == 0) {
                lines[++count] = $0
                next
            }
            lines[++count] = substr($0, 1, end - 1)
            found = 1
            exit
        }
        END {
            if (!found) {
                exit 1
            }
            first = lines[1]
            if (index(first, "TearDown : ") == 1) {
                first = substr(first, length("TearDown : ") + 1)
            }
            if (first == "Unhandled log message: \047[Error] " error "\047. Use UnityEngine.TestTools.LogAssert.Expect") {
                stack = 0
                rest = 2
            } else if (first == "Unhandled log message: [Error] " error && lines[2] == "Stack trace of the first:") {
                stack = 1
                rest = 3
            } else {
                exit 1
            }
            # The stack trace ends in a line break, so a failure that the
            # TearDown joins after it follows an empty line.
            blank = 0
            for (i = rest; i <= count; i++) {
                if (lines[i] == "") {
                    blank = 1
                } else if (!stack || blank) {
                    exit 1
                }
            }
            exit 0
        }
    ' "$results_file"
}

# Sets DATA_STORE_ENDED and DATA_STORE_EXIT_CODE to when the Data Store
# service whose process ID is given ended and with what exit code, as the
# helper recorded them for a run, or to nothing for what it did not record.
read_data_store_end() {
    local record_file="$1"
    local pid="$2"
    local key
    local value

    DATA_STORE_ENDED=""
    DATA_STORE_EXIT_CODE=""
    [[ -n "$pid" && -f "$record_file" ]] || return 0

    while IFS='=' read -r key value || [[ -n "$key" ]]; do
        value="${value%$'\r'}"
        case "$key" in
            "data_store_${pid}_ended") DATA_STORE_ENDED="$value" ;;
            "data_store_${pid}_exit_code") DATA_STORE_EXIT_CODE="$value" ;;
        esac
    done <"$record_file"
}

# Adds the end of Unity's Data Store service that find_data_store_loss found,
# if it found one, under the summary entry of the run that test_target ran
# last, whose results are the files that start with the base given: as the
# environment's failure when the test that was running failed on the error
# Unity logged for it and nothing else, given as 1, and as a warning
# otherwise. A line follows with when the service ended and its exit code, as
# the helper recorded them, and the service's log that the run kept.
add_data_store_loss_to_summary() {
    local failed_the_test="$1"
    local base="$2"
    local line="Unity's Data Store service (pid $DATA_STORE_LOSS_PID) ended"
    local kept_log="$base.uds-service.log"

    [[ -n "$DATA_STORE_LOSS_PID" ]] || return 0

    if [[ "$failed_the_test" -eq 1 ]]; then
        line="environment: $line while $DATA_STORE_LOSS_TEST ran, and the error Unity logged for it failed that test, so the test reached no verdict; run the target again"
    elif [[ -n "$DATA_STORE_LOSS_TEST" ]]; then
        line="warning: $line while $DATA_STORE_LOSS_TEST ran; Unity read the files it had imported in its own process from then on"
    else
        line="warning: $line while no test ran; Unity read the files it had imported in its own process from then on"
    fi
    printf '  %s\n' "$line" >&2
    SUMMARY_LINES+=("  $line")

    read_data_store_end "$base.load" "$DATA_STORE_LOSS_PID"
    if [[ -n "$DATA_STORE_ENDED" && -n "$DATA_STORE_EXIT_CODE" ]]; then
        line="pid $DATA_STORE_LOSS_PID exited with code $DATA_STORE_EXIT_CODE at $DATA_STORE_ENDED"
    elif [[ -n "$DATA_STORE_ENDED" ]]; then
        line="pid $DATA_STORE_LOSS_PID ended at $DATA_STORE_ENDED with an exit code that could not be read"
    elif [[ -f "$kept_log" && -f "$base.load" ]]; then
        line="pid $DATA_STORE_LOSS_PID has no end in ${base#"$REPO_ROOT"/}.load"
    elif [[ -f "$kept_log" ]]; then
        line="pid $DATA_STORE_LOSS_PID has no end recorded, since the run has no load record"
    else
        return 0
    fi
    if [[ -f "$kept_log" ]]; then
        line="$line; its log is kept as ${kept_log#"$REPO_ROOT"/}"
    fi
    printf '  data store service: %s\n' "$line" >&2
    SUMMARY_LINES+=("  data store service: $line")
}

# Called by run_unity_for_target once Unity has exited, before it removes an
# isolated mirror, with the folder Unity opened as the project. When Unity's
# log says that it launched its Data Store service, this keeps the log the
# service wrote there, Logs/UDS-Service.log, beside the run's results. Every
# such run keeps it, since Unity logs a service's end only as it next reads an
# imported file, so an end late in a run may never reach its log, and a
# service that ended with its Editor gives the log to compare with. In a run
# that launched no service, the log there is an earlier run's.
after_unity_run() {
    local service_log="$1/Logs/UDS-Service.log"
    local kept_log="${UNITY_LOG_FILE%.log}.uds-service.log"

    grep -q -F 'Launching Unity Data Store' "$UNITY_LOG_FILE" 2>/dev/null || return 0
    [[ -f "$service_log" ]] || return 0
    cp -- "$service_log" "$kept_log" 2>/dev/null \
        || printf 'warning: could not keep %s beside the results\n' "$service_log" >&2
}

record_failure() {
    local target_key="$1"
    local reason="$2"

    printf 'error: %s%s: %s\n' "$target_key" "$RUN_LABEL" "$reason" >&2
    SUMMARY_LINES+=("$target_key$RUN_LABEL: FAILED, $reason")
    FAILED_TARGET_COUNT=$((FAILED_TARGET_COUNT + 1))
}

# Sets LOAD_PYTHON to the first of python and python3 that runs Python 3.10 or
# later, the version mu3-cli needs, or LOAD_UNAVAILABLE to why the helper cannot
# run. python comes first: on Windows python3 is often the stand-in of the
# Microsoft Store, which only says how to install Python.
find_load_python() {
    local candidate

    LOAD_PYTHON=""
    LOAD_UNAVAILABLE=""
    if [[ ! -f "$LOAD_HELPER" ]]; then
        LOAD_UNAVAILABLE="${LOAD_HELPER#"$REPO_ROOT"/} is missing"
        return 1
    fi
    for candidate in python python3; do
        if command -v "$candidate" >/dev/null 2>&1 \
            && "$candidate" -I -c 'import sys; sys.exit(sys.version_info < (3, 10))' >/dev/null 2>&1; then
            LOAD_PYTHON="$candidate"
            return 0
        fi
    done
    LOAD_UNAVAILABLE="neither python nor python3 runs Python 3.10 or later"
    return 1
}

# Starts the helper for the run whose Unity log is UNITY_LOG_FILE, with its
# record and stop files beside it. It counts the Unity processes that do not
# descend from this script, whose Windows process ID it needs under Git Bash,
# and starts the spinners waiting, to spin from the first test the log shows.
start_load_helper() {
    local base="$1"
    local root_pid="$$"
    local idle=()

    LOAD_STOP_FILE="$base.load-stop"
    rm -f -- "$base.load" "$LOAD_STOP_FILE"
    [[ -n "$LOAD_PYTHON" ]] || return 0

    if [[ -r "/proc/$$/winpid" ]]; then
        read -r root_pid <"/proc/$$/winpid"
    fi
    if [[ "${UNITY_LOAD_SPINNERS_IDLE:-0}" == 1 ]]; then
        idle=(--idle)
    fi

    "$LOAD_PYTHON" -I "$(to_windows_path "$LOAD_HELPER")" supervise \
        --log "$(to_windows_path "$UNITY_LOG_FILE")" \
        --spinners "$LOAD_SPINNERS" \
        --root-pid "$root_pid" \
        --stop "$(to_windows_path "$LOAD_STOP_FILE")" \
        --record "$(to_windows_path "$base.load")" \
        ${idle[@]+"${idle[@]}"} </dev/null &
    LOAD_HELPER_PID=$!
}

# Tells the helper that the run ended, so that it stops the spinners and writes
# its record, and waits for it; one that has not ended after 10 s is stopped
# with every process it started. An exit, an interrupt included, calls it too.
stop_load_helper() {
    local helper_pid="$LOAD_HELPER_PID"
    local look

    LOAD_HELPER_PID=""
    [[ -n "$helper_pid" ]] || return 0

    : >"$LOAD_STOP_FILE" 2>/dev/null || true
    for ((look = 0; look < 100; look++)); do
        kill -0 "$helper_pid" 2>/dev/null || break
        sleep 0.1
    done
    if kill -0 "$helper_pid" 2>/dev/null; then
        stop_process_tree "$helper_pid"
    fi
    wait "$helper_pid" 2>/dev/null || true
    rm -f -- "$LOAD_STOP_FILE"
}

# Reads the key=value lines the helper wrote for a run into the LOAD_ and
# OTHERS_ variables, which stay empty for what it did not record.
read_load_record() {
    local record_file="$1"
    local key
    local value
    local complete=""

    LOAD_STATE=""
    LOAD_CPUS=""
    LOAD_ENDED_BEFORE=""
    LOAD_SECONDS=""
    LOAD_LAG_MS=""
    LOAD_ENDED=""
    LOAD_ERROR=""
    OTHERS_AT_START=""
    OTHERS_MIN=""
    OTHERS_MAX=""
    OTHERS_LOOKS=""
    COUNT_ERROR=""

    if [[ -z "$LOAD_PYTHON" ]]; then
        COUNT_ERROR="$LOAD_UNAVAILABLE"
        return 0
    fi
    if [[ ! -f "$record_file" ]]; then
        LOAD_ERROR="the helper wrote no record"
        COUNT_ERROR="$LOAD_ERROR"
        return 0
    fi

    while IFS='=' read -r key value || [[ -n "$key" ]]; do
        value="${value%$'\r'}"
        case "$key" in
            load) LOAD_STATE="$value" ;;
            cpus) LOAD_CPUS="$value" ;;
            tests_ended_before_load) LOAD_ENDED_BEFORE="$value" ;;
            load_seconds) LOAD_SECONDS="$value" ;;
            load_lag_ms) LOAD_LAG_MS="$value" ;;
            load_ended) LOAD_ENDED="$value" ;;
            error) LOAD_ERROR="$value" ;;
            others_at_start) OTHERS_AT_START="$value" ;;
            others_during_min) OTHERS_MIN="$value" ;;
            others_during_max) OTHERS_MAX="$value" ;;
            looks) OTHERS_LOOKS="$value" ;;
            count_error) COUNT_ERROR="$value" ;;
            complete) complete="$value" ;;
        esac
    done <"$record_file"

    # The helper writes its record as the run goes on, and marks the last one.
    if [[ "$complete" != yes ]]; then
        LOAD_ERROR="${LOAD_ERROR:-the helper ended before it completed its record}"
    fi
}

# Sets LOAD_PROBLEM to why a run whose tests passed does not count under
# --load-spinners, given its number of tests: the load failed, or no test ran
# with it.
find_load_problem() {
    local total="$1"

    LOAD_PROBLEM=""
    [[ "$LOAD_SPINNERS" -gt 0 ]] || return 0

    if [[ -n "$LOAD_ERROR" ]]; then
        LOAD_PROBLEM="the load failed: $LOAD_ERROR"
    elif [[ "$LOAD_STATE" == missed ]]; then
        LOAD_PROBLEM="the run ended before the load could start, so no test ran with it"
    elif [[ "$LOAD_STATE" != started ]]; then
        LOAD_PROBLEM="no test began in Unity's log, so none ran with the load"
    elif [[ "${LOAD_ENDED_BEFORE:-0}" -ge "$total" ]]; then
        LOAD_PROBLEM="every test had ended before the load started"
    fi
}

# Adds what the helper recorded for the run that test_target ran last under its
# summary entry: the load, and the other Unity processes.
add_load_to_summary() {
    local during
    local count
    local start_phrase
    local end_phrase="until Unity exited"

    if [[ "$LOAD_SPINNERS" -gt 0 ]]; then
        if [[ "${LOAD_ENDED_BEFORE:-0}" -gt 0 ]]; then
            start_phrase="started after $LOAD_ENDED_BEFORE test(s) had ended"
        elif [[ -n "$LOAD_LAG_MS" ]]; then
            start_phrase="from $LOAD_LAG_MS ms after the first test began"
        else
            start_phrase="from the first test"
        fi
        if [[ "$LOAD_ENDED" == last-test ]]; then
            end_phrase="until the last test ended"
        fi

        if [[ -n "$LOAD_ERROR" ]]; then
            SUMMARY_LINES+=("  load: failed, $LOAD_ERROR")
        elif [[ "$LOAD_STATE" == started ]]; then
            SUMMARY_LINES+=("  load: $LOAD_SPINNERS spinners on $LOAD_CPUS logical CPUs for $LOAD_SECONDS s, $start_phrase $end_phrase")
        elif [[ "$LOAD_STATE" == missed ]]; then
            SUMMARY_LINES+=("  load: none, the run ended before it could start")
        else
            SUMMARY_LINES+=("  load: none, no test began in Unity's log")
        fi
    fi

    if [[ -z "$OTHERS_AT_START" || -z "$OTHERS_LOOKS" ]]; then
        SUMMARY_LINES+=("  other Unity processes: not counted, ${COUNT_ERROR:-the helper recorded no count}")
        return 0
    fi
    if [[ "$OTHERS_MIN" == "$OTHERS_MAX" ]]; then
        during="$OTHERS_MIN"
    else
        during="$OTHERS_MIN to $OTHERS_MAX"
    fi
    SUMMARY_LINES+=("  other Unity processes: $OTHERS_AT_START at the start, $during while it ran ($OTHERS_LOOKS look(s))")

    for count in "$OTHERS_AT_START" "$OTHERS_MIN" "$OTHERS_MAX"; do
        if [[ -z "$REPEAT_OTHERS_LOW" || "$count" -lt "$REPEAT_OTHERS_LOW" ]]; then
            REPEAT_OTHERS_LOW="$count"
        fi
        if [[ -z "$REPEAT_OTHERS_HIGH" || "$count" -gt "$REPEAT_OTHERS_HIGH" ]]; then
            REPEAT_OTHERS_HIGH="$count"
        fi
    done
}

# Adds a test that had the result given, Failed or Inconclusive, in the run
# that test_target ran last to the totals of the target's runs.
tally_repeated_test() {
    local result="$1"
    local test_name="$2"
    local i

    for ((i = 0; i < ${#REPEAT_TEST_NAMES[@]}; i++)); do
        if [[ "${REPEAT_TEST_RESULTS[$i]}" == "$result" && "${REPEAT_TEST_NAMES[$i]}" == "$test_name" ]]; then
            REPEAT_TEST_RUNS[$i]=$((REPEAT_TEST_RUNS[$i] + 1))
            return 0
        fi
    done
    REPEAT_TEST_RESULTS+=("$result")
    REPEAT_TEST_NAMES+=("$test_name")
    REPEAT_TEST_RUNS+=(1)
}

# Adds the run that test_target ran last, whose results file is given and
# which failed when the second argument is 1, to the totals of its target.
add_run_to_repeat_totals() {
    local results_file="$1"
    local run_failed="$2"
    local i
    local result
    local test_name

    if [[ "$run_failed" -eq 1 ]]; then
        REPEAT_FAILED_RUNS=$((REPEAT_FAILED_RUNS + 1))
    fi
    [[ "${#RUN_COUNTS[@]}" -eq 5 ]] || return 0

    REPEAT_COUNTED_RUNS=$((REPEAT_COUNTED_RUNS + 1))
    for ((i = 0; i < 5; i++)); do
        REPEAT_SUMS[$i]=$((${REPEAT_SUMS[$i]:-0} + RUN_COUNTS[$i]))
    done
    for result in Failed Inconclusive; do
        find_tests_with_result "$results_file" "$result"
        for test_name in ${RESULT_TESTS[@]+"${RESULT_TESTS[@]}"}; do
            tally_repeated_test "$result" "$test_name"
        done
    done
}

# Adds the totals of a target's runs to the summary: how many runs failed, the
# sums of the counts of those that wrote results, the runs in which each test
# failed or was inconclusive, and the fewest and most other Unity processes.
add_repeat_totals_to_summary() {
    local target_key="$1"
    local counts="no run wrote results"
    local i

    if [[ "$REPEAT_COUNTED_RUNS" -gt 0 ]]; then
        counts="${REPEAT_SUMS[0]} tests, ${REPEAT_SUMS[1]} passed, ${REPEAT_SUMS[2]} failed, ${REPEAT_SUMS[3]} skipped, ${REPEAT_SUMS[4]} inconclusive"
        if [[ "$REPEAT_COUNTED_RUNS" -lt "$REPEAT_COUNT" ]]; then
            counts="$counts, in the $REPEAT_COUNTED_RUNS runs that wrote results"
        fi
    fi
    SUMMARY_LINES+=("$target_key, $REPEAT_COUNT runs: $REPEAT_FAILED_RUNS of $REPEAT_COUNT failed; $counts")

    for ((i = 0; i < ${#REPEAT_TEST_NAMES[@]}; i++)); do
        if [[ "${REPEAT_TEST_RESULTS[$i]}" == Failed ]]; then
            SUMMARY_LINES+=("  failed in ${REPEAT_TEST_RUNS[$i]} of $REPEAT_COUNT runs: ${REPEAT_TEST_NAMES[$i]}")
        else
            SUMMARY_LINES+=("  inconclusive in ${REPEAT_TEST_RUNS[$i]} of $REPEAT_COUNT runs: ${REPEAT_TEST_NAMES[$i]}")
        fi
    done

    if [[ "$REPEAT_DATA_STORE_LOSSES" -gt 0 ]]; then
        SUMMARY_LINES+=("  environment: Unity's Data Store service ended in $REPEAT_DATA_STORE_LOSSES of $REPEAT_COUNT runs, and $REPEAT_ENVIRONMENT_FAILED_RUNS of them failed on that alone")
    fi

    if [[ -n "$REPEAT_OTHERS_LOW" && "$REPEAT_OTHERS_LOW" == "$REPEAT_OTHERS_HIGH" ]]; then
        SUMMARY_LINES+=("  other Unity processes: $REPEAT_OTHERS_LOW at every count of the $REPEAT_COUNT runs")
    elif [[ -n "$REPEAT_OTHERS_LOW" ]]; then
        SUMMARY_LINES+=("  other Unity processes: $REPEAT_OTHERS_LOW to $REPEAT_OTHERS_HIGH over the $REPEAT_COUNT runs")
    fi
}

# Removes the results that an earlier --repeat left for a target, so that runs
# beyond this one's count are not read as its own.
remove_repeat_results() {
    local target_key="$1"
    local stale

    for stale in "$RESULTS_DIR/$target_key-$PLATFORM_KEY-run"[0-9]*; do
        case "$stale" in
            *.xml|*.log|*.load)
                rm -f -- "$stale"
                ;;
        esac
    done
}

# Runs a target REPEAT_COUNT times, each run with its own results, and adds the
# totals of its runs to the summary.
test_target_repeatedly() {
    local target_key="$1"
    local run
    local run_number
    local failed_before
    local environment_failed_before

    remove_repeat_results "$target_key"
    REPEAT_FAILED_RUNS=0
    REPEAT_COUNTED_RUNS=0
    REPEAT_SUMS=(0 0 0 0 0)
    REPEAT_TEST_RESULTS=()
    REPEAT_TEST_NAMES=()
    REPEAT_TEST_RUNS=()
    REPEAT_OTHERS_LOW=""
    REPEAT_OTHERS_HIGH=""
    REPEAT_DATA_STORE_LOSSES=0
    REPEAT_ENVIRONMENT_FAILED_RUNS=0

    for ((run = 1; run <= REPEAT_COUNT; run++)); do
        # Numbers as wide as the count keep the files of the runs in order.
        printf -v run_number '%0*d' "${#REPEAT_COUNT}" "$run"
        RUN_LABEL=" run $run of $REPEAT_COUNT"
        RUN_FILE_SUFFIX="-run$run_number"
        printf 'Run %s of %s of %s:\n' "$run" "$REPEAT_COUNT" "$target_key"
        failed_before="$FAILED_TARGET_COUNT"
        environment_failed_before="$ENVIRONMENT_FAILED_COUNT"
        test_target "$target_key"
        if [[ -n "$DATA_STORE_LOSS_PID" ]]; then
            REPEAT_DATA_STORE_LOSSES=$((REPEAT_DATA_STORE_LOSSES + 1))
        fi
        if [[ "$ENVIRONMENT_FAILED_COUNT" -gt "$environment_failed_before" ]]; then
            REPEAT_ENVIRONMENT_FAILED_RUNS=$((REPEAT_ENVIRONMENT_FAILED_RUNS + 1))
        fi
        add_path_limit_warnings_to_summary
        add_load_to_summary
        add_run_to_repeat_totals "$RESULTS_DIR/$target_key-$PLATFORM_KEY$RUN_FILE_SUFFIX.xml" \
            "$((FAILED_TARGET_COUNT > failed_before ? 1 : 0))"
    done
    RUN_LABEL=""
    RUN_FILE_SUFFIX=""

    add_repeat_totals_to_summary "$target_key"
}

test_target() {
    local target_key="$1"
    local results_file="$RESULTS_DIR/$target_key-$PLATFORM_KEY$RUN_FILE_SUFFIX.xml"
    local total
    local passed
    local failed
    local skipped
    local inconclusive
    local counts
    local unfinished
    local stall_phrase
    local loss_failed_test
    local unity_arguments=(-runTests -testPlatform "$TEST_PLATFORM")

    if [[ -n "$TEST_FILTER" ]]; then
        unity_arguments+=(-testFilter "$TEST_FILTER")
    fi

    mkdir -p "$RESULTS_DIR"
    # A result file left by an earlier run must not stand in for this one.
    if [[ -e "$results_file" || -L "$results_file" ]]; then
        rm -f -- "$results_file"
    fi
    UNITY_LOG_FILE="$RESULTS_DIR/$target_key-$PLATFORM_KEY$RUN_FILE_SUFFIX.log"
    # The helper starts the load at the first test this run's Unity logs, so a
    # log that an earlier run left must not be there for it to read, and the
    # Data Store service's log of an earlier run must not stand in either.
    rm -f -- "$UNITY_LOG_FILE" "${results_file%.xml}.uds-service.log"
    RUN_COUNTS=()
    start_load_helper "${results_file%.xml}"

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
    stop_load_helper
    read_load_record "${results_file%.xml}.load"
    find_unfinished_test "$UNITY_LOG_FILE"
    unfinished="$UNFINISHED_TEST"
    find_data_store_loss "$UNITY_LOG_FILE"

    if [[ "$UNITY_STALLED" -eq 1 ]]; then
        if [[ -n "$unfinished" ]]; then
            stall_phrase="while running $unfinished"
        else
            stall_phrase="while no test was running"
        fi
        record_failure "$target_key" "Unity wrote nothing to its log for $UNITY_STALL_SECONDS s $stall_phrase, so the script stopped it; see ${UNITY_LOG_FILE#"$REPO_ROOT"/}"
        add_data_store_loss_to_summary 0 "${results_file%.xml}"
        return 0
    fi

    if [[ ! -f "$results_file" ]]; then
        record_failure "$target_key" "Unity exited with $UNITY_STATUS and wrote no test results${unfinished:+ while running $unfinished}; see its log above"
        add_data_store_loss_to_summary 0 "${results_file%.xml}"
        return 0
    fi

    if ! read_test_run_counts "$results_file"; then
        record_failure "$target_key" "could not read the test counts in ${results_file#"$REPO_ROOT"/}"
        add_data_store_loss_to_summary 0 "${results_file%.xml}"
        return 0
    fi
    counts="$total tests, $passed passed, $failed failed, $skipped skipped, $inconclusive inconclusive"
    RUN_COUNTS=("$total" "$passed" "$failed" "$skipped" "$inconclusive")
    find_load_problem "$total"

    # Unity logs the end of its Data Store service as an error, which fails
    # the test then running whatever that test checks, so a failure on that
    # error alone gives no verdict. The target still fails, as for an
    # inconclusive test, but a run whose one failure it was says that it came
    # from the environment. A test whose failure message shows more failed on
    # its own, even while the error went by.
    loss_failed_test=0
    if [[ "$failed" -gt 0 ]] && failed_only_on_data_store_loss "$results_file"; then
        loss_failed_test=1
    fi

    # An inconclusive test, one whose Assume did not hold or that called
    # Assert.Inconclusive, reached no verdict, so it fails the target as a
    # failed test does; the Unity Test Framework exits with 2 for it too. A test
    # that cannot run on this machine calls Assert.Ignore, which NUnit counts as
    # skipped.
    if [[ "$loss_failed_test" -eq 1 && "$failed" -eq 1 && "$inconclusive" -eq 0 ]]; then
        record_failure "$target_key" "$counts; its one failure came from the environment"
        ENVIRONMENT_FAILED_COUNT=$((ENVIRONMENT_FAILED_COUNT + 1))
        name_failed_and_inconclusive_tests "$results_file"
    elif [[ "$failed" -gt 0 || "$inconclusive" -gt 0 ]]; then
        record_failure "$target_key" "$counts"
        name_failed_and_inconclusive_tests "$results_file"
    elif [[ "$total" -eq 0 && -n "$TEST_FILTER" ]]; then
        record_failure "$target_key" "no test matched --filter $TEST_FILTER"
    elif [[ "$total" -eq 0 ]]; then
        record_failure "$target_key" "no tests ran although the project lists testables"
    elif [[ "$UNITY_STATUS" -ne 0 ]]; then
        record_failure "$target_key" "Unity exited with $UNITY_STATUS although no test failed ($counts)"
    elif [[ -n "$LOAD_PROBLEM" ]]; then
        record_failure "$target_key" "$counts, but $LOAD_PROBLEM"
    else
        printf '%s tests passed for %s%s: %s\n' "$TEST_PLATFORM" "$target_key" "$RUN_LABEL" "$counts"
        SUMMARY_LINES+=("$target_key$RUN_LABEL: $counts")
    fi
    add_data_store_loss_to_summary "$loss_failed_test" "${results_file%.xml}"
}

# Adds the warnings that run_unity_for_target gave about the Windows path limit
# for the target it ran last under that target's summary entry, since Unity's
# log follows them and the run passes regardless.
add_path_limit_warnings_to_summary() {
    local warning

    for warning in "$PATH_LIMIT_WARNING" "$UNREADABLE_PATHS_WARNING"; do
        if [[ -n "$warning" ]]; then
            SUMMARY_LINES+=("  warning: $warning")
        fi
    done
}

print_summary() {
    local line

    printf '%s test summary:\n' "$TEST_PLATFORM"
    if [[ "${#SUMMARY_LINES[@]}" -gt 0 ]]; then
        for line in "${SUMMARY_LINES[@]}"; do
            # A line that starts with a space details the target line before it.
            if [[ "$line" == " "* ]]; then
                printf '  %s\n' "$line"
            else
                printf '  - %s\n' "$line"
            fi
        done
    fi
}

test_main() {
    local target_key
    local preflight_failed
    local results_name
    local environment_phrase=""

    load_package_config
    parse_test_arguments "$@"
    parse_arguments ${COMPILE_ARGUMENTS[@]+"${COMPILE_ARGUMENTS[@]}"}
    select_requested_targets
    print_selection
    if [[ -n "$TEST_FILTER" ]]; then
        printf 'Test filter: %s\n' "$TEST_FILTER"
    fi
    if [[ "$LOAD_SPINNERS" -gt 0 ]]; then
        printf 'Load: %s spinners, from the first test of each run until its last test ends\n' "$LOAD_SPINNERS"
    fi
    if [[ "$REPEAT_COUNT" -gt 1 ]]; then
        printf 'Repeat: %s runs of each target\n' "$REPEAT_COUNT"
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

    # The helper that a run starts stops with the script, however it ends.
    trap 'stop_load_helper; cleanup_staging' EXIT

    preflight_failed=0
    preflight_selected_targets || preflight_failed=1
    if ! find_load_python; then
        if [[ "$LOAD_SPINNERS" -gt 0 ]]; then
            printf 'error: --load-spinners needs its helper, but %s\n' "$LOAD_UNAVAILABLE" >&2
            preflight_failed=1
        else
            printf 'warning: other Unity processes will not be counted: %s\n' "$LOAD_UNAVAILABLE" >&2
        fi
    fi
    [[ "$preflight_failed" -eq 0 ]] || fail "preflight failed; no tests were run"

    for target_key in "${SELECTED_TARGETS[@]}"; do
        if [[ "$REPEAT_COUNT" -gt 1 ]]; then
            test_target_repeatedly "$target_key"
        else
            test_target "$target_key"
            add_path_limit_warnings_to_summary
            add_load_to_summary
        fi
    done

    print_summary
    if [[ "$REPEAT_COUNT" -gt 1 ]]; then
        results_name="<target>-$PLATFORM_KEY-run<k>"
    else
        results_name="<target>-$PLATFORM_KEY"
    fi
    printf 'Results: log/unity-tests/%s.xml, with Unity logs as %s.log%s\n' \
        "$results_name" "$results_name" "${LOAD_PYTHON:+ and load records as $results_name.load}"

    if [[ "$ENVIRONMENT_FAILED_COUNT" -gt 0 ]]; then
        environment_phrase="; $ENVIRONMENT_FAILED_COUNT of them failed only on the environment, as the summary says"
    fi
    if [[ "$FAILED_TARGET_COUNT" -gt 0 && "$REPEAT_COUNT" -gt 1 ]]; then
        fail "$TEST_PLATFORM tests failed in $FAILED_TARGET_COUNT of $((${#SELECTED_TARGETS[@]} * REPEAT_COUNT)) runs$environment_phrase"
    elif [[ "$FAILED_TARGET_COUNT" -gt 0 ]]; then
        fail "$TEST_PLATFORM tests failed for $FAILED_TARGET_COUNT target(s)$environment_phrase"
    fi
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    test_main "$@"
fi
