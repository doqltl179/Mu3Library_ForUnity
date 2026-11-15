#!/usr/bin/env bash

# Compiles, with the Unity Editor in batch mode, the Unity projects generated
# from the configs in tools/unity-projects. Other entry points source this file
# for its target selection, Editor lookup, lock check, and project generation;
# compile_main at the bottom runs only when it is executed.

set -euo pipefail

# Every command substitution, pipeline, and external command costs Git Bash a
# process, about 10 to 20 ms, which adds up over a run and over the tool tests;
# so the paths every run takes find targets, read files, and pass results back
# in this shell where that is no harder, and keep processes for real work.

# The folder of this file, cut from its path as dirname cuts it: the dirname of
# Git Bash and Cygwin also takes \ as a separator, which a Windows path that
# bash was given holds.
case "${OSTYPE:-}" in
    msys*|cygwin*) SCRIPT_DIR="${BASH_SOURCE[0]%[/\\]*}" ;;
    *) SCRIPT_DIR="${BASH_SOURCE[0]%/*}" ;;
esac
if [[ "$SCRIPT_DIR" == "${BASH_SOURCE[0]}" ]]; then
    SCRIPT_DIR="."
fi
SCRIPT_DIR="$(cd "${SCRIPT_DIR:-/}" && pwd)"
readonly SCRIPT_DIR
readonly REPO_ROOT="$SCRIPT_DIR"
# One folder per config; each config is a target of the same name.
readonly CONFIGS_DIR="$REPO_ROOT/tools/unity-projects"
# Reads the configs, the checkout packages their manifests name, and the work
# folder, and generates a config's project; it runs with nothing but the
# standard library (tools/cli/README.md).
readonly PROJECTS_TOOL="$REPO_ROOT/tools/cli/src/mu3_common/unity_projects.py"

# The word the selection messages use for what the entry point does with a target.
SELECTION_LABEL="compile"
TARGET="changed"
TARGET_WAS_SET=0
BASE_REF=""
# An Editor version that replaces each project's own, set by --editor-version.
EDITOR_VERSION=""
DRY_RUN=0
# The folder that holds the generated projects: --work-dir, or else
# ${MU3_WORK_ROOT}/Mu3Library_ForUnity/<unit>, as load_project_configs reads it,
# or why there is none.
WORK_DIR_ARGUMENT=""
WORK_DIR=""
WORK_DIR_ERROR=""
# The Python of version 3.10 or later that runs the tools, found by find_tools_python.
TOOLS_PYTHON=""
RUN_NOTE=""
UNITY_STATUS=0
readonly COMPILE_LOG_DIR="$REPO_ROOT/log/unity-compile"
# run_unity_for_target keeps Unity's log in UNITY_LOG_FILE, which each entry
# point names for its target, and stops Unity once that log has not grown for
# UNITY_STALL_SECONDS (0 never stops it), setting UNITY_STALLED. --stall-timeout
# sets the seconds; test-unity.sh sets its own default. A compile's default is
# about 12 times the longest silence measured in a first import, where script
# compilation logged nothing for up to 25 s (tools/README.md).
UNITY_LOG_FILE=""
UNITY_STALL_SECONDS=300
UNITY_STALLED=0
# How often, in seconds, run_unity_logged looks at that log. A real run keeps
# 1; the tool tests set UNITY_LOG_POLL_SECONDS in the environment to a fraction
# so that a fake Unity, which exits at once, does not wait out a whole second.
UNITY_LOG_POLL_SECONDS="${UNITY_LOG_POLL_SECONDS:-1}"
# The stall watch's clock, which read_unity_clock sets: the time in
# microseconds, and how much shorter than the difference of two readings the
# time between them may be.
UNITY_CLOCK=0
UNITY_CLOCK_SLACK=0
# On Windows Unity's asset import cannot read a file whose full path has
# WINDOWS_PATH_LIMIT characters or more, even with long paths enabled, and the
# run still succeeds. LONGEST_PROJECT_FILE_PATH is the longest path below a
# project folder that Unity could not read in the measured deep runs
# (tools/README.md), Library\PackageCache\com.unity.render-pipelines.core@...
# \RenderPipelineConverterVisualElementListFilter.uxml, so a project folder
# whose path reaches the limit with it loses package files.
readonly WINDOWS_PATH_LIMIT=260
readonly LONGEST_PROJECT_FILE_PATH=161
# run_unity_for_target leaves its warnings about that limit for the target it
# ran last here: about the project folder, before Unity starts, and about the
# files Unity's log says it could not read, after it exits. COMPILE_WARNINGS
# keeps every one of a compile run, which compile_main repeats once every target
# has compiled; a failed compile ends the run right after its own warnings.
PATH_LIMIT_WARNING=""
UNREADABLE_PATHS_WARNING=""
COMPILE_WARNINGS=()
# The helper that runs beside each run of a target, the Python that runs it,
# found by find_load_python, or why there is none. It records when each Unity
# Data Store service of the run started and ended and its exit code, and counts
# the Unity processes that are not the run's own; test-unity.sh also has it put
# its runs under a fixed load.
readonly LOAD_HELPER="$REPO_ROOT/tools/cli/src/mu3_fixed_load/fixed_load.py"
LOAD_PYTHON=""
LOAD_UNAVAILABLE=""
LOAD_HELPER_PID=""
LOAD_STOP_FILE=""

# One entry per config, at the index of its name in TARGET_KEYS: the Editor
# version its ProjectVersion.txt names, 1 when its manifest lists testables,
# the folder its project is generated in (empty without a work folder), and the
# checkout package folders its manifest names, separated by spaces.
TARGET_KEYS=()
CONFIG_EDITOR_VERSIONS=()
CONFIG_TESTABLES=()
PROJECT_FOLDERS=()
CONFIG_PACKAGES=()
# Every checkout package folder, and at the same index the configs whose
# manifest names it, separated by spaces.
PACKAGE_FOLDERS=()
PACKAGE_CONSUMER_TARGETS=()
CHANGED_FILES=()
RELEVANT_CHANGES=()
IGNORED_CHANGES=()
SELECTED_TARGETS=()
# Results that functions leave in these variables instead of printing them, so
# that their callers need no command substitution.
HOST_PLATFORM=""
TARGET_INDEX=0
FOUND_UNITY_PATH=""
HUB_EDITOR_SUFFIX=""
PROJECT_LOCK_STATE=""

fail() {
    printf 'error: %s\n' "$*" >&2
    exit 1
}

host_platform() {
    case "$(uname -s)" in
        Darwin)
            printf 'macos\n'
            ;;
        MINGW*|MSYS*|CYGWIN*)
            printf 'windows\n'
            ;;
        *)
            printf 'linux\n'
            ;;
    esac
}

# Sets HOST_PLATFORM to what host_platform prints, asking it only once in a
# shell.
detect_host_platform() {
    if [[ -z "$HOST_PLATFORM" ]]; then
        HOST_PLATFORM="$(host_platform)"
    fi
}

to_windows_path() {
    local path="$1"

    if command -v cygpath >/dev/null 2>&1; then
        cygpath -w "$path"
        return
    fi

    printf '%s\n' "$path"
}

# Sets TARGET_INDEX to the index of a configured target in the mapping arrays,
# or returns 1.
find_target() {
    local requested_target="$1"
    local i

    for ((i = 0; i < ${#TARGET_KEYS[@]}; i++)); do
        if [[ "${TARGET_KEYS[$i]}" == "$requested_target" ]]; then
            TARGET_INDEX="$i"
            return 0
        fi
    done

    return 1
}

# Sets TOOLS_PYTHON to the first of python and python3 that runs Python 3.10
# or later, the version tools/cli needs, or fails: it reads the configs and
# generates the projects. python comes first: on Windows python3 is often the
# stand-in of the Microsoft Store, which only says how to install Python.
find_tools_python() {
    local candidate

    [[ -z "$TOOLS_PYTHON" ]] || return 0
    for candidate in python python3; do
        if command -v "$candidate" >/dev/null 2>&1 \
            && "$candidate" -I -c 'import sys; sys.exit(sys.version_info < (3, 10))' >/dev/null 2>&1; then
            TOOLS_PYTHON="$candidate"
            return 0
        fi
    done
    fail "neither python nor python3 runs Python 3.10 or later, which reads tools/unity-projects and generates the Unity projects"
}

# Runs the tool that reads the configs and generates the projects, with the
# repository root given and its output in UTF-8, whatever the console's code
# page.
run_projects_tool() {
    local command="$1"

    shift
    find_tools_python
    "$TOOLS_PYTHON" -I -X utf8 "$(to_windows_path "$PROJECTS_TOOL")" "$command" \
        --root "$(to_windows_path "$REPO_ROOT")" "$@"
}

# Reads every config, the checkout package folders its manifest names, and its
# project folder under the work folder into the arrays above, as the tool
# describes them in lines of tab-separated fields. A config that the tool cannot
# read stops the script with the tool's error.
load_project_configs() {
    local arguments=()
    local output
    local line
    local rest
    local fields
    local consumers
    local i
    local j

    if [[ -n "$WORK_DIR_ARGUMENT" ]]; then
        arguments+=(--work-dir "$(to_windows_path "$WORK_DIR_ARGUMENT")")
    fi
    if [[ -n "$EDITOR_VERSION" ]]; then
        arguments+=(--editor-version "$EDITOR_VERSION")
    fi
    output="$(run_projects_tool describe ${arguments[@]+"${arguments[@]}"})" || exit 1

    while IFS= read -r line; do
        # Python on Windows ends each line with CR LF. A field may be empty, so
        # the line is split at each tab rather than by read, which would merge
        # two tabs into one.
        rest="${line%$'\r'}"
        fields=()
        while [[ "$rest" == *$'\t'* ]]; do
            fields+=("${rest%%$'\t'*}")
            rest="${rest#*$'\t'}"
        done
        fields+=("$rest")
        case "${fields[0]}" in
            package)
                PACKAGE_FOLDERS+=("${fields[1]}")
                ;;
            config)
                TARGET_KEYS+=("${fields[1]}")
                CONFIG_EDITOR_VERSIONS+=("${fields[2]}")
                CONFIG_TESTABLES+=("${fields[3]}")
                PROJECT_FOLDERS+=("${fields[4]}")
                CONFIG_PACKAGES+=("${fields[5]}")
                ;;
            work-dir)
                WORK_DIR="${fields[1]}"
                ;;
            work-dir-error)
                WORK_DIR_ERROR="${fields[1]}"
                ;;
        esac
    done <<< "$output"

    [[ "${#TARGET_KEYS[@]}" -gt 0 ]] || fail "no project configs found in ${CONFIGS_DIR#"$REPO_ROOT"/}"

    # A package compiles in every config whose manifest names it, and each of
    # those configs installs different optional packages, so it gives the
    # shared sources a different define set. A change to the package therefore
    # has to compile in all of them.
    PACKAGE_CONSUMER_TARGETS=()
    for ((i = 0; i < ${#PACKAGE_FOLDERS[@]}; i++)); do
        consumers=""
        for ((j = 0; j < ${#TARGET_KEYS[@]}; j++)); do
            if [[ " ${CONFIG_PACKAGES[$j]} " == *" ${PACKAGE_FOLDERS[$i]} "* ]]; then
                consumers="${consumers:+$consumers }${TARGET_KEYS[$j]}"
            fi
        done
        PACKAGE_CONSUMER_TARGETS+=("$consumers")
    done
}

usage() {
    local config_dir

    cat <<'EOF'
Usage: ./compile-unity.sh [changed|all|target] [options]

Selection:
  changed          Compile every project whose config names a package affected
                   by Git changes, or whose config changed (default).
  all              Compile the project of every config.
EOF

    for config_dir in "$CONFIGS_DIR"/*/; do
        [[ -d "$config_dir" ]] || continue
        config_dir="${config_dir%/}"
        printf '  %-16s Compile the project of tools/unity-projects/%s\n' "${config_dir##*/}" "${config_dir##*/}"
    done

    cat <<'EOF'

Options:
  --base <git-ref>  Include committed changes from <git-ref>...HEAD.
  --dry-run         Print changed files, selected targets, and the project
                    folder each target would generate or refresh, without
                    Unity and without creating anything.
  --work-dir <folder>
                    Generate the projects in <folder>/<target> instead of
                    ${MU3_WORK_ROOT}/Mu3Library_ForUnity/<unit>/<target>.
  --editor-version <version>
                    Run with the Unity Hub Editor of that exact version, such
                    as 6000.3.17f1, instead of the one ProjectVersion.txt
                    names, in a project of its own, <target>-<version>,
                    retargeted to it.
  --stall-timeout <seconds>
                    Stop Unity and fail the target once its log has not
                    grown for this long (default 300; 0 waits as long as
                    Unity runs).
  -h, --help        Show this help.

Environment:
  MU3_WORK_ROOT
      Folder outside the checkout that holds the work folders, such as D:/w.
      Without --work-dir the projects go to
      ${MU3_WORK_ROOT}/Mu3Library_ForUnity/<unit>/<target>, where <unit> is
      the first 32 characters of the current branch with slashes as dashes
      and without trailing dots; a run with neither, or on a detached HEAD
      without --work-dir, stops before Unity starts.
  UNITY_EDITOR or UNITY
      Optional path to a Unity executable (or Unity.app) that overrides the
      project version lookup. Without an override, the script reads each
      config's ProjectVersion.txt and uses the matching Unity Hub installation.
  UNITY_HUB_EDITOR_ROOT
      Optional folder that holds one folder per installed Editor version, for
      a Unity Hub set to install Editors outside its default location
      (Program Files\Unity\Hub\Editor, /Applications/Unity/Hub/Editor, or
      ~/Unity/Hub/Editor). mu3-unity doctor reads the same variable.

Each target is a config in tools/unity-projects. Before Unity starts, the
script generates the target's project from its config, or refreshes the one
an earlier run generated: it writes each file of the config that differs,
removes each file an earlier run wrote that the config no longer holds, points
each package the manifest names as "checkout" at its folder in this checkout,
and keeps Library/ for the next run. It needs python or python3 of version
3.10 or later from PATH. It refuses a project folder it did not make, and a
project that an Editor has open: a project counts as open while a process holds
its Temp/UnityLockfile, and a lock file that no process holds, left by an
Editor that crashed, is reported and ignored. On Windows PowerShell checks the
holder, elsewhere lsof does; without either, an existing lock file stops the
target.

Changed mode includes staged, unstaged, and untracked files. With --base it
also includes committed changes from the requested ref through HEAD. A changed
package selects every config whose Packages/manifest.json names it, because
each project compiles it with different defines, and a change inside
tools/unity-projects/<target> selects that target.

--editor-version checks a package against another Editor, such as the lowest
one its package.json declares. The project's ProjectVersion.txt names the
requested version, its packages-lock.json is left out, and each Unity package
version in its manifest becomes the one that Editor bundles; a built-in module
the Editor does not ship is dropped, and a package it does not bundle stays as
it is.

On Windows Unity cannot import a package file whose path has 260 characters or
more, and the run does not fail for it. Before Unity starts, the script warns
when the project folder has 98 characters or more; after Unity exits, it warns
when the log names files Unity could not read for that reason. Once every
target of a run of several has compiled, the script repeats the warnings; a
failed compile ends the run at once, right after its own warnings and error.
Keep MU3_WORK_ROOT or --work-dir short; see tools/README.md.

The script prints Unity's log as Unity writes it and keeps it in
log/unity-compile/<target>.log. A compile that stops writing, such as one
waiting on a dialog that batch mode cannot show, is stopped with its child
processes after --stall-timeout seconds and fails its target.

Beside that log, a helper records in <target>.load when each Unity Data Store
service of the run, the process that serves the Editor the files it imported,
started and ended and its exit code, and a run whose Unity launched the service
keeps the log the service wrote, the project's Logs/UDS-Service.log, as
<target>.uds-service.log. The script judges neither; without the helper, it
warns and records no service.
EOF
}

add_changed_file() {
    local candidate="$1"
    local existing

    if [[ "${#CHANGED_FILES[@]}" -gt 0 ]]; then
        for existing in "${CHANGED_FILES[@]}"; do
            if [[ "$existing" == "$candidate" ]]; then
                return
            fi
        done
    fi

    CHANGED_FILES+=("$candidate")
}

collect_changed_files() {
    local changed_file
    local base_commit

    git -C "$REPO_ROOT" rev-parse --is-inside-work-tree >/dev/null 2>&1 \
        || fail "changed selection requires a Git worktree"

    if [[ -n "$BASE_REF" ]]; then
        base_commit="$(git -C "$REPO_ROOT" rev-parse --verify "$BASE_REF^{commit}" 2>/dev/null)" \
            || fail "Git base ref not found: $BASE_REF"
        git -C "$REPO_ROOT" merge-base "$base_commit" HEAD >/dev/null 2>&1 \
            || fail "Git base ref has no merge base with HEAD: $BASE_REF"

        while IFS= read -r -d '' changed_file; do
            add_changed_file "$changed_file"
        done < <(git -C "$REPO_ROOT" diff --name-only --no-renames -z "$base_commit...HEAD")
    fi

    while IFS= read -r -d '' changed_file; do
        add_changed_file "$changed_file"
    done < <(git -C "$REPO_ROOT" diff --name-only --no-renames -z)

    while IFS= read -r -d '' changed_file; do
        add_changed_file "$changed_file"
    done < <(git -C "$REPO_ROOT" diff --cached --name-only --no-renames -z)

    while IFS= read -r -d '' changed_file; do
        add_changed_file "$changed_file"
    done < <(git -C "$REPO_ROOT" ls-files --others --exclude-standard -z)
}

select_target() {
    local requested_target="$1"
    local selected_target

    find_target "$requested_target" \
        || fail "unknown $SELECTION_LABEL target: $requested_target"

    if [[ "${#SELECTED_TARGETS[@]}" -gt 0 ]]; then
        for selected_target in "${SELECTED_TARGETS[@]}"; do
            if [[ "$selected_target" == "$requested_target" ]]; then
                return
            fi
        done
    fi

    SELECTED_TARGETS+=("$requested_target")
}

select_all_targets() {
    local target_key

    for target_key in "${TARGET_KEYS[@]}"; do
        select_target "$target_key"
    done
}

is_package_compile_relevant() {
    local package_relative_path="$1"

    case "$package_relative_path" in
        README*|CHANGELOG*|LICENSE*|Documentation~/*|*.md)
            return 1
            ;;
    esac

    return 0
}

# Prints the targets a changed path selects on one line, separated by spaces, or "all".
targets_for_changed_path() {
    local changed_path="$1"
    local package_path
    local config
    local i

    for ((i = 0; i < ${#PACKAGE_FOLDERS[@]}; i++)); do
        package_path="${PACKAGE_FOLDERS[$i]}"
        if [[ "$changed_path" == "$package_path/"* ]]; then
            # A package that no config names compiles nowhere.
            if ! is_package_compile_relevant "${changed_path#"$package_path"/}" \
                || [[ -z "${PACKAGE_CONSUMER_TARGETS[$i]}" ]]; then
                return 1
            fi
            printf '%s\n' "${PACKAGE_CONSUMER_TARGETS[$i]}"
            return 0
        fi
    done

    case "$changed_path" in
        tools/unity-projects/*/*)
            config="${changed_path#tools/unity-projects/}"
            config="${config%%/*}"
            if find_target "$config"; then
                printf '%s\n' "$config"
                return 0
            fi
            return 1
            ;;
        tools/*|docs/*|.github/*)
            return 1
            ;;
        *.cs|*.asmdef|*.asmref)
            printf 'all\n'
            return 0
            ;;
    esac

    return 1
}

select_changed_targets() {
    local changed_file
    local mapped_line
    local mapped_targets
    local mapped_target
    local mapped_label

    collect_changed_files

    if [[ "${#CHANGED_FILES[@]}" -eq 0 ]]; then
        return
    fi

    for changed_file in "${CHANGED_FILES[@]}"; do
        if mapped_line="$(targets_for_changed_path "$changed_file")"; then
            IFS=' ' read -r -a mapped_targets <<< "$mapped_line"
            mapped_label=""
            for mapped_target in "${mapped_targets[@]}"; do
                if [[ "$mapped_target" == "all" ]]; then
                    select_all_targets
                else
                    select_target "$mapped_target"
                fi
                mapped_label="${mapped_label:+$mapped_label, }$mapped_target"
            done
            RELEVANT_CHANGES+=("$changed_file"$'\t'"$mapped_label")
        else
            IGNORED_CHANGES+=("$changed_file")
        fi
    done
}

print_selection() {
    local change_entry
    local changed_path
    local mapped_target
    local selected_target
    local packages

    if [[ "$TARGET" == "changed" ]]; then
        printf 'Git changes inspected: %d\n' "${#CHANGED_FILES[@]}"
        if [[ -n "$BASE_REF" ]]; then
            printf 'Git base: %s...HEAD plus local changes\n' "$BASE_REF"
        else
            printf 'Git base: local staged, unstaged, and untracked changes\n'
        fi

        if [[ "${#RELEVANT_CHANGES[@]}" -gt 0 ]]; then
            printf 'Unity-relevant changes:\n'
            for change_entry in "${RELEVANT_CHANGES[@]}"; do
                IFS=$'\t' read -r changed_path mapped_target <<< "$change_entry"
                printf '  - %s -> %s\n' "$changed_path" "$mapped_target"
            done
        fi

        if [[ "$DRY_RUN" -eq 1 && "${#IGNORED_CHANGES[@]}" -gt 0 ]]; then
            printf 'Ignored non-Unity changes:\n'
            for changed_path in "${IGNORED_CHANGES[@]}"; do
                printf '  - %s\n' "$changed_path"
            done
        fi
    else
        printf 'Selection mode: explicit target %s\n' "$TARGET"
    fi

    if [[ "${#SELECTED_TARGETS[@]}" -eq 0 ]]; then
        printf 'Selected %s targets: none\n' "$SELECTION_LABEL"
        return
    fi

    printf 'Selected %s targets:\n' "$SELECTION_LABEL"
    for selected_target in "${SELECTED_TARGETS[@]}"; do
        find_target "$selected_target"
        packages="${CONFIG_PACKAGES[$TARGET_INDEX]// /, }"
        printf '  - %s: tools/unity-projects/%s with %s\n' \
            "$selected_target" "$selected_target" "${packages:-no checkout package}"
    done
}

# Unity Hub's install location, which holds one folder per Editor version.
# UNITY_HUB_EDITOR_ROOT replaces it for a Hub set to install Editors elsewhere;
# mu3-unity doctor reads the same variable.
hub_editor_root() {
    local editor_root="${UNITY_HUB_EDITOR_ROOT:-}"
    local program_files

    if [[ -n "$editor_root" ]]; then
        if [[ "$editor_root" == "~" || "$editor_root" == "~/"* ]]; then
            editor_root="$HOME${editor_root:1}"
        fi
        detect_host_platform
        if [[ "$HOST_PLATFORM" == "windows" ]] && command -v cygpath >/dev/null 2>&1; then
            editor_root="$(cygpath -u "$editor_root")"
        fi
        printf '%s\n' "${editor_root%/}"
        return
    fi

    detect_host_platform
    case "$HOST_PLATFORM" in
        macos)
            printf '/Applications/Unity/Hub/Editor\n'
            ;;
        windows)
            if command -v cygpath >/dev/null 2>&1; then
                program_files="$(cygpath -u "${PROGRAMFILES:-C:\\Program Files}")"
            else
                program_files="/c/Program Files"
            fi
            printf '%s/Unity/Hub/Editor\n' "${program_files%/}"
            ;;
        *)
            printf '%s/Unity/Hub/Editor\n' "$HOME"
            ;;
    esac
}

hub_editor_executable() {
    local required_version="$1"
    local editor_root

    editor_root="$(hub_editor_root)"
    detect_host_platform
    case "$HOST_PLATFORM" in
        macos)
            printf '%s/%s/Unity.app/Contents/MacOS/Unity\n' "$editor_root" "$required_version"
            ;;
        windows)
            printf '%s/%s/Editor/Unity.exe\n' "$editor_root" "$required_version"
            ;;
        *)
            printf '%s/%s/Editor/Unity\n' "$editor_root" "$required_version"
            ;;
    esac
}

# Sets HUB_EDITOR_SUFFIX to the trailing part of a Hub installation path, used
# to tell whether an explicit override still points at the Editor version the
# project asks for.
hub_editor_suffix() {
    local required_version="$1"

    detect_host_platform
    case "$HOST_PLATFORM" in
        macos)
            HUB_EDITOR_SUFFIX="/Editor/$required_version/Unity.app/Contents/MacOS/Unity"
            ;;
        windows)
            HUB_EDITOR_SUFFIX="/Editor/$required_version/Editor/Unity.exe"
            ;;
        *)
            HUB_EDITOR_SUFFIX="/Editor/$required_version/Editor/Unity"
            ;;
    esac
}

# Sets FOUND_UNITY_PATH to the Unity executable for a target, or prints why
# there is none and returns 1. It warns when an explicit override is not the
# Editor the target asks for, unless the second argument is quiet: the
# preflight warns once for each target, before any Unity starts, so the run
# that follows passes quiet.
unity_executable() {
    local target_key="$1"
    local warn_about_override=1
    local required_version
    local override_path="${UNITY_EDITOR:-${UNITY:-}}"
    local candidate
    # Who asks for the version, as the messages below name it.
    local requester

    if [[ "${2:-}" == "quiet" ]]; then
        warn_about_override=0
    fi
    FOUND_UNITY_PATH=""
    find_target "$target_key" || return 1
    requester="tools/unity-projects/$target_key"
    if [[ -n "$EDITOR_VERSION" ]]; then
        required_version="$EDITOR_VERSION"
        requester="--editor-version"
    else
        # The tool read it from the config's ProjectSettings/ProjectVersion.txt.
        required_version="${CONFIG_EDITOR_VERSIONS[$TARGET_INDEX]}"
    fi

    if [[ -n "$override_path" ]]; then
        candidate="$override_path"
        if [[ -d "$candidate" && "$candidate" == *.app ]]; then
            candidate="$candidate/Contents/MacOS/Unity"
        fi

        if [[ ! -x "$candidate" ]]; then
            printf 'error: Unity override is not executable: %s\n' "$candidate" >&2
            return 1
        fi

        hub_editor_suffix "$required_version"
        if [[ "$warn_about_override" -eq 1 && "$candidate" != *"$HUB_EDITOR_SUFFIX" ]]; then
            printf 'warning: %s requires Unity %s but the explicit override will be used: %s\n' \
                "$requester" "$required_version" "$candidate" >&2
        fi
    else
        candidate="$(hub_editor_executable "$required_version")"
        if [[ ! -x "$candidate" ]]; then
            printf 'error: required Unity Editor is not installed for %s: %s\n' \
                "$requester" "$candidate" >&2
            return 1
        fi
    fi

    FOUND_UNITY_PATH="$candidate"
}

# Prints held when some process has the file open, free when none does, gone
# when the file no longer exists, or unknown when this host cannot tell.
file_holder_state() {
    local file="$1"
    local quoted_path
    local holders
    local probe_status=0

    detect_host_platform
    case "$HOST_PLATFORM" in
        windows)
            if ! command -v powershell.exe >/dev/null 2>&1; then
                printf 'unknown\n'
                return 0
            fi

            # Opening the file without sharing fails with a sharing violation
            # (HRESULT 0x80070020) for as long as any other handle is open.
            quoted_path="$(to_windows_path "$file")"
            quoted_path=${quoted_path//\'/\'\'}
            powershell.exe -NoLogo -NoProfile -NonInteractive -Command "
                try {
                    [System.IO.File]::Open('$quoted_path', 'Open', 'Read', 'None').Dispose()
                    exit 0
                } catch {
                    \$cause = \$_.Exception
                    while (\$cause.InnerException) { \$cause = \$cause.InnerException }
                    if (\$cause.HResult -eq -2147024864) { exit 3 }
                    if (\$cause -is [System.IO.FileNotFoundException] -or \$cause -is [System.IO.DirectoryNotFoundException]) { exit 4 }
                    exit 5
                }" >/dev/null 2>&1 || probe_status=$?

            case "$probe_status" in
                0) printf 'free\n' ;;
                3) printf 'held\n' ;;
                4) printf 'gone\n' ;;
                *) printf 'unknown\n' ;;
            esac
            ;;
        *)
            if ! command -v lsof >/dev/null 2>&1; then
                printf 'unknown\n'
                return 0
            fi

            holders="$(lsof -t -- "$file" 2>/dev/null || true)"
            if [[ -n "$holders" ]]; then
                printf 'held\n'
            elif [[ -e "$file" ]]; then
                printf 'free\n'
            else
                printf 'gone\n'
            fi
            ;;
    esac
}

# Sets PROJECT_LOCK_STATE to absent, open, stale, or unknown for the
# Temp/UnityLockfile of a project folder. A running Editor keeps the file open,
# so a lock file that no process holds was left behind by an Editor that
# crashed or was killed.
project_lock_state() {
    local project_folder="$1"
    local lock_file="$project_folder/Temp/UnityLockfile"

    if [[ ! -e "$lock_file" ]]; then
        PROJECT_LOCK_STATE="absent"
        return 0
    fi

    case "$(file_holder_state "$lock_file")" in
        held) PROJECT_LOCK_STATE="open" ;;
        free) PROJECT_LOCK_STATE="stale" ;;
        gone) PROJECT_LOCK_STATE="absent" ;;
        *) PROJECT_LOCK_STATE="unknown" ;;
    esac
}

# Decides whether a target's project can be generated and run: returns 1, with
# the reason in RUN_NOTE, when there is no work folder or an Editor may hold the
# project, whose files a refresh would change under it. A stale lock file only
# leaves a note in RUN_NOTE.
project_run_problem() {
    local target_key="$1"
    local project_folder

    RUN_NOTE=""
    if [[ -n "$WORK_DIR_ERROR" ]]; then
        RUN_NOTE="$WORK_DIR_ERROR"
        return 1
    fi
    find_target "$target_key" || return 1
    project_folder="${PROJECT_FOLDERS[$TARGET_INDEX]}"

    project_lock_state "$project_folder"
    case "$PROJECT_LOCK_STATE" in
        open)
            RUN_NOTE="$project_folder is open in Unity; close it, since the run refreshes the project's files"
            return 1
            ;;
        unknown)
            # Closing an Editor does not help here: the lock may be stale, and only
            # deleting it, or a way to check its holder, lets the run go on.
            detect_host_platform
            if [[ "$HOST_PLATFORM" == "windows" ]]; then
                RUN_NOTE="$project_folder/Temp/UnityLockfile exists and PowerShell could not open it to check whether an Editor holds it; delete the file if no Editor has the project open"
            else
                RUN_NOTE="$project_folder/Temp/UnityLockfile exists and no lsof is installed to check whether an Editor holds it; install lsof, or delete the file if no Editor has the project open"
            fi
            return 1
            ;;
        stale)
            RUN_NOTE="ignoring a stale $project_folder/Temp/UnityLockfile that no running process holds"
            ;;
    esac
}

# Prints the project folder a dry run would generate or refresh for a target,
# or why it cannot run.
project_plan_phrase() {
    local target_key="$1"
    local action="refresh"

    if ! project_run_problem "$target_key"; then
        printf 'cannot run: %s\n' "$RUN_NOTE"
        return 0
    fi
    if [[ ! -e "${PROJECT_FOLDERS[$TARGET_INDEX]}" ]]; then
        action="generate"
    fi
    printf '%s %s%s\n' "$action" "${PROJECT_FOLDERS[$TARGET_INDEX]}" "${RUN_NOTE:+, $RUN_NOTE}"
}

# One target's line in the dry-run plan; an entry point that sources this file
# can replace it to add its own conditions.
run_plan_entry() {
    project_plan_phrase "$1"
}

print_run_plan() {
    local target_key

    [[ "${#SELECTED_TARGETS[@]}" -gt 0 ]] || return 0

    printf 'Run plan:\n'
    for target_key in "${SELECTED_TARGETS[@]}"; do
        # run_plan_entry prints the rest of the line.
        printf '  - %s: ' "$target_key"
        run_plan_entry "$target_key" || true
    done
}

# Sets PATH_LIMIT_WARNING to a warning, or to nothing, about the folder that
# Unity opens as the project: on Windows, when the longest path measured below
# a project reaches WINDOWS_PATH_LIMIT there. Unity also clones git-URL package
# dependencies into Library/PackageCache, which a path this long can break as
# well. Bash counts the folder's length by the locale, as
# find_unreadable_long_paths says of awk.
check_project_path_length() {
    local project_folder="$1"
    local windows_path
    local longest_path
    # A file path holds the folder, a separator, and the path below it.
    local longest_folder=$((WINDOWS_PATH_LIMIT - 2 - LONGEST_PROJECT_FILE_PATH))

    PATH_LIMIT_WARNING=""
    detect_host_platform
    [[ "$HOST_PLATFORM" == "windows" ]] || return 0

    windows_path="$(to_windows_path "$project_folder")"
    longest_path=$((${#windows_path} + 1 + LONGEST_PROJECT_FILE_PATH))
    [[ "$longest_path" -ge "$WINDOWS_PATH_LIMIT" ]] || return 0

    PATH_LIMIT_WARNING="the project path $windows_path is ${#windows_path} characters, so the longest package file path measured below a project reaches $longest_path there, and Unity cannot import a file whose path has $WINDOWS_PATH_LIMIT characters or more on Windows; set a shorter MU3_WORK_ROOT or pass a shorter --work-dir, so that the project path has at most $longest_folder characters (tools/README.md)"
}

# Sets UNREADABLE_PATHS_WARNING to a warning, or to nothing, about the files that
# Unity's log says it could not find at paths of WINDOWS_PATH_LIMIT characters or
# more, and the asset imports that failed on them, naming each DLL among them:
# on Windows only, the one host with that limit. With -automated Unity logs each
# error again in a ##utp: line, which is left out, and a file or an asset that
# fails twice counts once.
# awk counts length() in characters under a UTF-8 locale and in bytes otherwise,
# as the gawk 5.0.0 of Git Bash does without LANG. Bytes never fall short of the
# UTF-16 units that Windows counts, so a non-ASCII path can only be over-counted
# and warned about early; characters match those units except outside the Basic
# Multilingual Plane.
find_unreadable_long_paths() {
    local log_file="$1"
    local files=""
    local imports=""
    local dlls=""

    UNREADABLE_PATHS_WARNING=""
    detect_host_platform
    [[ "$HOST_PLATFORM" == "windows" ]] || return 0
    [[ -f "$log_file" ]] || return 0

    {
        read -r files imports || true
        IFS= read -r dlls || true
    } < <(awk -v limit="$WINDOWS_PATH_LIMIT" '
        BEGIN {
            marker = "DirectoryNotFoundException: Could not find a part of the path \""
            import_marker = "Asset import failed, \""
        }
        /^##utp:/ { next }
        {
            start = index($0, marker)
            if (start == 0) {
                next
            }
            rest = substr($0, start + length(marker))
            end = index(rest, "\"")
            if (end == 0) {
                next
            }
            path = substr(rest, 1, end - 1)
            if (length(path) < limit) {
                next
            }
            if (!(path in seen_files)) {
                seen_files[path] = 1
                file_count++
                name = path
                sub(/.*\\/, "", name)
                if (tolower(name) ~ /\.dll$/) {
                    dlls = dlls (dlls == "" ? "" : ", ") name
                }
            }
            if (index($0, import_marker) == 1) {
                asset = substr($0, length(import_marker) + 1)
                asset = substr(asset, 1, index(asset, "\"") - 1)
                if (!(asset in seen_assets)) {
                    seen_assets[asset] = 1
                    import_count++
                }
            }
        }
        END {
            print file_count + 0, import_count + 0
            print dlls
        }' "$log_file" 2>/dev/null || true)

    [[ "${files:-0}" -gt 0 ]] || return 0
    UNREADABLE_PATHS_WARNING="Unity could not read $files file(s) whose paths have $WINDOWS_PATH_LIMIT characters or more${dlls:+, among them $dlls}"
    if [[ "${imports:-0}" -gt 0 ]]; then
        UNREADABLE_PATHS_WARNING+=", and $imports asset import(s) failed on them"
    fi
    UNREADABLE_PATHS_WARNING+="; see ${log_file#"$REPO_ROOT"/}"
}

# Generates a target's project from its config, or refreshes the one an earlier
# run generated, in its folder under the work folder; with --editor-version
# the project names that Editor and takes the Unity package versions that the
# Editor at the given path bundles. Fails when the tool does, as when the folder
# is not one it made.
generate_project() {
    local target_key="$1"
    local unity_path="$2"
    local editor_arguments=()

    find_target "$target_key" || return 1
    if [[ -n "$EDITOR_VERSION" ]]; then
        editor_arguments=(--editor-version "$EDITOR_VERSION" --editor "$(to_windows_path "$unity_path")")
    fi
    run_projects_tool generate --config "$target_key" \
        --project "$(to_windows_path "${PROJECT_FOLDERS[$TARGET_INDEX]}")" \
        ${editor_arguments[@]+"${editor_arguments[@]}"} \
        || fail "the project of $target_key could not be generated in ${PROJECT_FOLDERS[$TARGET_INDEX]}"
}

# Prints the processes that descend from a process and share its process
# group, one PID a line. Git Bash lists the processes of every session, and a
# process whose parent ended keeps that parent's PID, which a new process can
# take; the group keeps another session's processes out.
process_descendants() {
    local root="$1"
    local processes

    if [[ -r "/proc/$$/winpid" ]]; then
        # Git Bash's ps prints PID, PPID, and PGID first, after a status letter
        # on some lines; a Windows program it started is listed under the PID
        # it gave it.
        processes="$(ps -e | awk 'NR > 1 { if ($1 ~ /^[0-9]+$/) print $1, $2, $3; else print $2, $3, $4 }')"
    else
        processes="$(ps -A -o pid= -o ppid= -o pgid=)"
    fi

    awk -v root="$root" '
        { parent[$1] = $2; group[$1] = $3 }
        END {
            if (!(root in group)) {
                exit
            }
            found[root] = 1
            do {
                added = 0
                for (pid in parent) {
                    if (!(pid in found) && group[pid] == group[root] && (parent[pid] in found)) {
                        found[pid] = 1
                        added = 1
                    }
                }
            } while (added)
            for (pid in found) {
                if (pid != root) {
                    print pid
                }
            }
        }' <<<"$processes"
}

# Stops a process that this script started and every process descending from
# it, which it lists first, since a process whose parent ended loses its place
# in the tree.
stop_process_tree() {
    local root="$1"
    local pid
    local processes

    read -r -d '' -a processes < <(process_descendants "$root" || true) || true
    for pid in "$root" ${processes[@]+"${processes[@]}"}; do
        # A Windows program ends through taskkill, which needs its Windows PID;
        # /T also ends the child processes it started itself, which Git Bash
        # does not list.
        if [[ -r "/proc/$pid/winpid" ]] && command -v taskkill >/dev/null 2>&1 \
            && taskkill //F //T //PID "$(cat "/proc/$pid/winpid")" >/dev/null 2>&1; then
            continue
        fi
        kill -KILL "$pid" 2>/dev/null || true
    done
}

# Sets LOAD_PYTHON to the Python that runs the helper, the one find_tools_python
# finds, or LOAD_UNAVAILABLE to why the helper cannot run.
find_load_python() {
    LOAD_PYTHON=""
    LOAD_UNAVAILABLE=""
    if [[ ! -f "$LOAD_HELPER" ]]; then
        LOAD_UNAVAILABLE="${LOAD_HELPER#"$REPO_ROOT"/} is missing"
        return 1
    fi
    find_tools_python
    LOAD_PYTHON="$TOOLS_PYTHON"
}

# Starts the helper for the run whose Unity log is UNITY_LOG_FILE, with its
# record, <base>.load, and its stop file beside it. It first removes what an
# earlier run left there, that record and the Data Store service's log that
# keep_data_store_service_log keeps as <base>.uds-service.log, before the run
# can fail, as when its project cannot be generated, so that neither stands in for
# this run's; a file it cannot remove, such as one another program holds open,
# gets a warning and does not stop the run. The helper watches the run's Data
# Store service and counts the Unity processes that do not descend from this
# script, whose Windows process ID it needs under Git Bash. Given a number of
# spinners, it starts them waiting, to spin from the first test the log shows;
# the tool tests set UNITY_LOAD_SPINNERS_IDLE=1 so that they wait instead.
start_load_helper() {
    local base="$1"
    local spinners="${2:-0}"
    local root_pid="$$"
    local idle=()
    local earlier

    LOAD_STOP_FILE="$base.load-stop"
    for earlier in "$base.load" "$LOAD_STOP_FILE" "$base.uds-service.log"; do
        rm -f -- "$earlier" 2>/dev/null \
            || printf 'warning: could not remove %s, which an earlier run left\n' "${earlier#"$REPO_ROOT"/}" >&2
    done
    [[ -n "$LOAD_PYTHON" ]] || return 0

    if [[ -r "/proc/$$/winpid" ]]; then
        read -r root_pid <"/proc/$$/winpid"
    fi
    if [[ "${UNITY_LOAD_SPINNERS_IDLE:-0}" == 1 ]]; then
        idle=(--idle)
    fi

    "$LOAD_PYTHON" -I "$(to_windows_path "$LOAD_HELPER")" supervise \
        --log "$(to_windows_path "$UNITY_LOG_FILE")" \
        --spinners "$spinners" \
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
    # A stop file left here only stops the next helper at once, and
    # start_load_helper warns that it cannot remove it.
    rm -f -- "$LOAD_STOP_FILE" 2>/dev/null || true
}

# Sets UNITY_CLOCK to the time in microseconds. Bash 5 reads it to the
# microsecond from EPOCHREALTIME, whose decimal separator follows the locale.
# An older bash, such as the 3.2 that macOS ships, has only SECONDS, which
# counts whole seconds, so two of its readings N seconds apart may be little
# more than N - 1 seconds apart in fact; UNITY_CLOCK_SLACK says by how much.
read_unity_clock() {
    local stamp="${EPOCHREALTIME:-}"

    if [[ -n "$stamp" ]]; then
        stamp="${stamp//[!0-9]/}"
        UNITY_CLOCK=$((10#$stamp))
        UNITY_CLOCK_SLACK=0
    else
        UNITY_CLOCK=$((SECONDS * 1000000))
        UNITY_CLOCK_SLACK=1000000
    fi
}

# Succeeds when the log, which has not grown since the reading $1 of
# read_unity_clock, has surely stood still for UNITY_STALL_SECONDS by the
# reading in UNITY_CLOCK. Taking the clock's slack off keeps the watch from
# stopping Unity before that time, however coarse the clock is.
unity_log_stalled() {
    local last_growth="$1"

    [[ "$UNITY_STALL_SECONDS" -gt 0 ]] \
        && [[ $((UNITY_CLOCK - last_growth - UNITY_CLOCK_SLACK)) -ge $((UNITY_STALL_SECONDS * 1000000)) ]]
}

# Runs a Unity command with its standard output in UNITY_LOG_FILE, prints the
# log as it grows, and returns Unity's exit status. A log that has not grown for
# UNITY_STALL_SECONDS stops Unity and sets UNITY_STALLED, so a compile or a test
# that never ends fails its target instead of holding the script forever.
run_unity_logged() {
    local unity_pid
    local status
    local size
    local shown=0
    local last_growth

    "$@" >"$UNITY_LOG_FILE" &
    unity_pid=$!
    read_unity_clock
    last_growth="$UNITY_CLOCK"
    # Unity runs in the background now, so an interrupt must stop it as well.
    # shellcheck disable=SC2064
    trap "stop_process_tree $unity_pid; exit 130" INT TERM

    while kill -0 "$unity_pid" 2>/dev/null; do
        sleep "$UNITY_LOG_POLL_SECONDS"
        # The clock is read before the log is measured, so a silence it times
        # has been seen whole, and again once growth is printed, so the growth
        # is never dated earlier than the script saw it.
        read_unity_clock
        size=$(($(wc -c <"$UNITY_LOG_FILE")))
        if [[ "$size" -gt "$shown" ]]; then
            tail -c "+$((shown + 1))" "$UNITY_LOG_FILE" | head -c "$((size - shown))"
            shown="$size"
            read_unity_clock
            last_growth="$UNITY_CLOCK"
        elif unity_log_stalled "$last_growth"; then
            UNITY_STALLED=1
            stop_process_tree "$unity_pid"
            break
        fi
    done

    wait "$unity_pid"
    status=$?
    trap - INT TERM
    tail -c "+$((shown + 1))" "$UNITY_LOG_FILE"
    return "$status"
}

# Generates or refreshes one target's project, then runs Unity in batch mode on
# it with the arguments that follow the verb and its log in UNITY_LOG_FILE, and
# leaves its exit code in UNITY_STATUS. A target whose project cannot run or be
# generated stops the script.
run_unity_for_target() {
    local target_key="$1"
    local verb="$2"
    local unity_path
    local project_folder
    local unity_command

    shift 2
    find_target "$target_key" || return 1
    project_folder="${PROJECT_FOLDERS[$TARGET_INDEX]}"
    unity_executable "$target_key" quiet
    unity_path="$FOUND_UNITY_PATH"

    if ! project_run_problem "$target_key"; then
        fail "$target_key: $RUN_NOTE"
    fi
    generate_project "$target_key" "$unity_path"
    printf '%s %s in %s%s.\n' "$verb" "$target_key" "$project_folder" "${RUN_NOTE:+, $RUN_NOTE}"

    printf 'Unity: %s\n' "$unity_path"
    printf 'Project: %s\n' "$project_folder"
    check_project_path_length "$project_folder"
    if [[ -n "$PATH_LIMIT_WARNING" ]]; then
        printf 'warning: %s: %s\n' "$target_key" "$PATH_LIMIT_WARNING" >&2
    fi

    unity_command=("$unity_path" -batchmode -projectPath "$project_folder" "$@" -logFile -)
    UNITY_STALLED=0
    set +e
    run_unity_logged "${unity_command[@]}"
    UNITY_STATUS=$?
    set -e

    find_unreadable_long_paths "$UNITY_LOG_FILE"
    if [[ -n "$UNREADABLE_PATHS_WARNING" ]]; then
        printf 'warning: %s: %s\n' "$target_key" "$UNREADABLE_PATHS_WARNING" >&2
    fi

    keep_data_store_service_log "$project_folder"
}

# Keeps the log that Unity's Data Store service wrote in the folder Unity
# opened as the project, Logs/UDS-Service.log, beside Unity's log as
# <name>.uds-service.log, when Unity's log says that it launched the service.
# Every such run keeps it, since Unity logs a service's end only as it next reads an imported
# file, so an end late in a run may never reach its log, and a service that
# ended with its Editor gives the log to compare with. In a run that launched
# no service, the log there is an earlier run's. start_load_helper removes the
# one an earlier run kept before the run starts.
keep_data_store_service_log() {
    local service_log="$1/Logs/UDS-Service.log"
    local kept_log="${UNITY_LOG_FILE%.log}.uds-service.log"

    grep -q -F 'Launching Unity Data Store' "$UNITY_LOG_FILE" 2>/dev/null || return 0
    [[ -f "$service_log" ]] || return 0
    cp -- "$service_log" "$kept_log" 2>/dev/null \
        || printf 'warning: could not keep %s beside the results\n' "$service_log" >&2
}

compile_target() {
    local target_key="$1"
    local warning

    mkdir -p "$COMPILE_LOG_DIR"
    UNITY_LOG_FILE="$COMPILE_LOG_DIR/$target_key.log"
    # The helper records the run's Data Store service in <target>.load, as for
    # a test run; a compile judges neither the service nor the other Unity
    # processes the helper counts.
    start_load_helper "$COMPILE_LOG_DIR/$target_key"
    run_unity_for_target "$target_key" "Compiling" -quit
    stop_load_helper
    for warning in "$PATH_LIMIT_WARNING" "$UNREADABLE_PATHS_WARNING"; do
        if [[ -n "$warning" ]]; then
            COMPILE_WARNINGS+=("$target_key: $warning")
        fi
    done

    if [[ "$UNITY_STALLED" -eq 1 ]]; then
        printf 'error: Unity compilation failed for %s: Unity wrote nothing to its log for %s s, so the script stopped it; see %s\n' \
            "$target_key" "$UNITY_STALL_SECONDS" "${UNITY_LOG_FILE#"$REPO_ROOT"/}" >&2
        return 1
    fi

    if [[ "$UNITY_STATUS" -ne 0 ]]; then
        printf 'error: Unity compilation failed for %s (exit %d)\n' "$target_key" "$UNITY_STATUS" >&2
        return "$UNITY_STATUS"
    fi

    printf 'Unity compilation succeeded: %s\n' "$target_key"
}

parse_arguments() {
    while [[ "$#" -gt 0 ]]; do
        case "$1" in
            --base)
                shift
                [[ "$#" -gt 0 ]] || fail "--base requires a Git ref"
                BASE_REF="$1"
                ;;
            --dry-run)
                DRY_RUN=1
                ;;
            --work-dir)
                shift
                [[ "$#" -gt 0 && -n "$1" ]] || fail "--work-dir requires a folder"
                WORK_DIR_ARGUMENT="$1"
                ;;
            --editor-version)
                shift
                [[ "$#" -gt 0 ]] || fail "--editor-version requires a Unity version"
                # The version names a Unity Hub folder, so it may hold nothing else.
                [[ "$1" =~ ^[0-9]+\.[0-9]+\.[0-9]+[a-z][0-9]+([a-z][0-9]+)?$ ]] \
                    || fail "--editor-version expects a full Unity version such as 6000.3.17f1: $1"
                EDITOR_VERSION="$1"
                ;;
            --stall-timeout)
                shift
                [[ "$#" -gt 0 && "$1" =~ ^[0-9]+$ ]] || fail "--stall-timeout expects a whole number of seconds"
                UNITY_STALL_SECONDS="$((10#$1))"
                ;;
            -h|--help)
                usage
                exit 0
                ;;
            --*)
                fail "unknown option: $1"
                ;;
            *)
                [[ "$TARGET_WAS_SET" -eq 0 ]] || fail "only one $SELECTION_LABEL target may be specified"
                TARGET="$1"
                TARGET_WAS_SET=1
                ;;
        esac
        shift
    done

    if [[ -n "$BASE_REF" && "$TARGET" != "changed" ]]; then
        fail "--base can only be used with the changed target"
    fi
}

select_requested_targets() {
    if [[ "$TARGET" == "changed" ]]; then
        select_changed_targets
    elif [[ "$TARGET" == "all" ]]; then
        select_all_targets
    else
        select_target "$TARGET"
    fi
}

# Checks every selected target before Unity starts, so that a missing Editor,
# a missing work folder, or a project open in an Editor does not surface only
# after earlier targets have spent minutes running. It is also the one place
# that warns about an explicit Editor that a target does not ask for.
preflight_selected_targets() {
    local target_key
    local preflight_failed=0

    for target_key in "${SELECTED_TARGETS[@]}"; do
        if ! unity_executable "$target_key"; then
            preflight_failed=1
        fi
    done

    if [[ -n "$WORK_DIR_ERROR" ]]; then
        printf 'error: %s\n' "$WORK_DIR_ERROR" >&2
        preflight_failed=1
    else
        for target_key in "${SELECTED_TARGETS[@]}"; do
            if ! project_run_problem "$target_key"; then
                printf 'error: %s: %s\n' "$target_key" "$RUN_NOTE" >&2
                preflight_failed=1
            fi
        done
    fi

    # A value sleep refuses, or 0, would turn the stall watch into a busy loop.
    if ! [[ "$UNITY_LOG_POLL_SECONDS" =~ ^[0-9]*\.?[0-9]+$ && "$UNITY_LOG_POLL_SECONDS" =~ [1-9] ]]; then
        printf 'error: UNITY_LOG_POLL_SECONDS expects a positive number of seconds: %s\n' "$UNITY_LOG_POLL_SECONDS" >&2
        preflight_failed=1
    fi

    [[ "$preflight_failed" -eq 0 ]]
}

compile_main() {
    local target_key

    parse_arguments "$@"
    load_project_configs
    select_requested_targets
    print_selection

    if [[ "$DRY_RUN" -eq 1 ]]; then
        # Without a work folder no target has a project folder to name.
        if [[ "${#SELECTED_TARGETS[@]}" -gt 0 && -n "$WORK_DIR_ERROR" ]]; then
            fail "$WORK_DIR_ERROR"
        fi
        print_run_plan
        exit 0
    fi

    if [[ "${#SELECTED_TARGETS[@]}" -eq 0 ]]; then
        printf 'No Unity package or project config changes detected; nothing to compile.\n'
        exit 0
    fi

    # The helper that a run starts stops with the script, however it ends.
    trap 'stop_load_helper' EXIT

    preflight_selected_targets || fail "preflight failed; no projects were compiled"
    if ! find_load_python; then
        printf 'warning: the Data Store service of each run will not be recorded: %s\n' "$LOAD_UNAVAILABLE" >&2
    fi

    for target_key in "${SELECTED_TARGETS[@]}"; do
        compile_target "$target_key"
    done

    # Each later target's Unity log follows a target's warnings, so a run of
    # several targets gives them all again once the last one has compiled.
    if [[ "${#SELECTED_TARGETS[@]}" -gt 1 && "${#COMPILE_WARNINGS[@]}" -gt 0 ]]; then
        printf 'warning: %s\n' "${COMPILE_WARNINGS[@]}" >&2
    fi
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    compile_main "$@"
fi
