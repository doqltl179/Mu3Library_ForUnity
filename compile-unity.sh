#!/usr/bin/env bash

# Compiles the development projects with the Unity Editor in batch mode.
# Other entry points source this file for its target selection, Editor lookup,
# and isolated mirror; compile_main at the bottom runs only when it is executed.

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly REPO_ROOT="$SCRIPT_DIR"
readonly PACKAGE_CONFIG="$REPO_ROOT/unity-cli-packages.tsv"

# The word the selection messages use for what the entry point does with a target.
SELECTION_LABEL="compile"
TARGET="changed"
TARGET_WAS_SET=0
BASE_REF=""
RUN_MODE="auto"
DRY_RUN=0
KEEP_STAGING=0
STAGING_ROOT=""
RUN_PROJECT_PATH=""
RUN_LOCATION=""
RUN_NOTE=""
UNITY_STATUS=0

SKIP_SAMPLES=0
readonly LINK_SAMPLES_COMMAND="PYTHONPATH=tools/cli/src python -m mu3_cli unity link-samples"

TARGET_KEYS=()
PACKAGE_PATHS=()
PROJECT_PATHS=()
# Project-relative path of the link to the package's Samples~ folder, or empty.
SAMPLE_LINKS=()
PACKAGE_CONSUMER_TARGETS=()
CHANGED_FILES=()
RELEVANT_CHANGES=()
IGNORED_CHANGES=()
SELECTED_TARGETS=()

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

to_windows_path() {
    local path="$1"

    if command -v cygpath >/dev/null 2>&1; then
        cygpath -w "$path"
        return
    fi

    printf '%s\n' "$path"
}

target_index() {
    local requested_target="$1"
    local i

    for ((i = 0; i < ${#TARGET_KEYS[@]}; i++)); do
        if [[ "${TARGET_KEYS[$i]}" == "$requested_target" ]]; then
            printf '%d\n' "$i"
            return 0
        fi
    done

    return 1
}

load_package_config() {
    local line_number=0
    local target_key
    local package_path
    local project_path
    local samples_link
    local extra

    [[ -f "$PACKAGE_CONFIG" ]] || fail "package mapping not found: $PACKAGE_CONFIG"

    while IFS=$'\t' read -r target_key package_path project_path samples_link extra \
        || [[ -n "$target_key$package_path$project_path$samples_link$extra" ]]; do
        line_number=$((line_number + 1))

        if [[ -z "$target_key" || "$target_key" == \#* ]]; then
            continue
        fi

        if [[ -z "$package_path" || -z "$project_path" || -n "$extra" ]]; then
            fail "invalid package mapping at $PACKAGE_CONFIG:$line_number"
        fi
        if [[ ! "$target_key" =~ ^[a-z0-9][a-z0-9-]*$ || "$target_key" == "changed" || "$target_key" == "all" ]]; then
            fail "invalid or reserved target '$target_key' at $PACKAGE_CONFIG:$line_number"
        fi
        if [[ "$package_path" == /* || "$project_path" == /*
            || "/$package_path/" == *"/../"* || "/$project_path/" == *"/../"* ]]; then
            fail "package and project paths must remain inside the repository at $PACKAGE_CONFIG:$line_number"
        fi
        if [[ "$samples_link" == /* || "/$samples_link/" == *"/../"* ]]; then
            fail "the samples link must be a path inside the project at $PACKAGE_CONFIG:$line_number"
        fi
        if target_index "$target_key" >/dev/null; then
            fail "duplicate target '$target_key' at $PACKAGE_CONFIG:$line_number"
        fi
        [[ -d "$REPO_ROOT/$package_path" ]] || fail "package directory not found: $REPO_ROOT/$package_path"
        [[ -d "$REPO_ROOT/$project_path" ]] || fail "Unity project directory not found: $REPO_ROOT/$project_path"

        TARGET_KEYS+=("$target_key")
        PACKAGE_PATHS+=("$package_path")
        PROJECT_PATHS+=("$project_path")
        SAMPLE_LINKS+=("$samples_link")
    done < "$PACKAGE_CONFIG"

    [[ "${#TARGET_KEYS[@]}" -gt 0 ]] || fail "no package mappings found in $PACKAGE_CONFIG"
}

# The closing quote keeps a package from matching one whose path only starts with it.
project_references_package() {
    local project_path="$1"
    local package_path="$2"
    local manifest_file="$REPO_ROOT/$project_path/Packages/manifest.json"

    [[ -f "$manifest_file" ]] || return 1
    grep -Fq "\"file:../../$package_path\"" "$manifest_file"
}

# A package compiles in every development project whose manifest references it, and
# each of those projects installs different optional packages, so it gives the shared
# sources a different define set. A change to the package therefore has to compile in
# all of them, not only in the project the mapping pairs it with. Each entry lists the
# targets for the package at the same index, separated by spaces.
load_package_consumers() {
    local consumers
    local i
    local j

    PACKAGE_CONSUMER_TARGETS=()
    for ((i = 0; i < ${#TARGET_KEYS[@]}; i++)); do
        consumers=""
        for ((j = 0; j < ${#TARGET_KEYS[@]}; j++)); do
            if [[ "$j" -eq "$i" ]] || project_references_package "${PROJECT_PATHS[$j]}" "${PACKAGE_PATHS[$i]}"; then
                consumers="${consumers:+$consumers }${TARGET_KEYS[$j]}"
            fi
        done
        PACKAGE_CONSUMER_TARGETS+=("$consumers")
    done
}

usage() {
    local i

    cat <<'EOF'
Usage: ./compile-unity.sh [changed|all|target] [options]

Selection:
  changed          Compile every project that consumes a package affected by
                   Git changes (default).
  all              Compile every configured package project.
EOF

    for ((i = 0; i < ${#TARGET_KEYS[@]}; i++)); do
        printf '  %-16s Compile %s for %s\n' \
            "${TARGET_KEYS[$i]}" "${PACKAGE_PATHS[$i]}" "${PROJECT_PATHS[$i]}"
    done

    cat <<'EOF'

Options:
  --base <git-ref>  Include committed changes from <git-ref>...HEAD.
  --dry-run         Print changed files, selected targets, and where each
                    target would run, without Unity.
  --isolated        Always compile a temporary source-only project mirror.
  --in-place        Compile the repository project directly; fail if it is open.
  --keep-staging    Keep an isolated project after the run for diagnostics.
  --without-samples Run even when a project's link to its package samples is
                    missing, leaving the samples out with a warning.
  -h, --help        Show this help.

Environment:
  UNITY_EDITOR or UNITY
      Optional path to a Unity executable (or Unity.app) that overrides the
      project version lookup. Without an override, the script reads each
      ProjectVersion.txt and uses the matching Unity Hub installation.
  UNITY_HUB_EDITOR_ROOT
      Optional folder that holds one folder per installed Editor version, for
      a Unity Hub set to install Editors outside its default location
      (Program Files\Unity\Hub\Editor, /Applications/Unity/Hub/Editor, or
      ~/Unity/Hub/Editor). mu3-cli unity doctor reads the same variable.
  TMPDIR
      Where isolated mirrors are created. Keep it short on Windows.

Changed mode includes staged, unstaged, and untracked files. With --base it
also includes committed changes from the requested ref through HEAD. Package
ownership and development projects are configured in unity-cli-packages.tsv.
A changed package selects every configured project whose Packages/manifest.json
references it, because each project compiles it with different defines.

Unity skips a package's Samples~ folder, so a project compiles the samples only
through the link the fourth column of unity-cli-packages.tsv names. A run stops
before Unity starts when a selected project lacks that link; create the links
with: PYTHONPATH=tools/cli/src python -m mu3_cli unity link-samples

The default execution mode compiles in place when a project is closed. If the
same project is open in Unity, it compiles a temporary mirror instead, leaving
the running editor, repository packages, and repository Library untouched.
A project counts as open while a process holds its Temp/UnityLockfile; a lock
file that no process holds, left by an Editor that crashed, is reported and
ignored. On Windows PowerShell checks the holder, elsewhere lsof does; without
either, an existing lock file counts as open.
EOF
}

# Removes a checked staging root. On Windows a Unity child process can keep
# files of the mirror open for a moment after Unity exits, so removal retries
# briefly. Prints the last removal error and returns 1 when it gives up.
remove_staging_root() {
    local staging_root="$1"
    local attempt
    local removal_error=""

    for attempt in 1 2 3; do
        if removal_error="$(rm -rf -- "$staging_root" 2>&1)"; then
            return 0
        fi
        if [[ "$attempt" -lt 3 ]]; then
            sleep "$attempt"
        fi
    done

    printf '%s\n' "${removal_error%%$'\n'*}"
    return 1
}

# Removes the isolated mirror of the last run. It never fails: the run's own
# result decides the exit status, and a mirror it cannot remove only warns.
cleanup_staging() {
    local staging_root="$STAGING_ROOT"
    local removal_error
    STAGING_ROOT=""
    RUN_PROJECT_PATH=""

    if [[ -z "$staging_root" ]]; then
        return 0
    fi

    if [[ "$KEEP_STAGING" -eq 1 ]]; then
        printf 'Kept isolated project: %s\n' "$staging_root"
        return 0
    fi

    if [[ ! -f "$staging_root/.mu3library-unity-compile" ]]; then
        printf 'warning: refusing to remove unrecognized staging path: %s\n' "$staging_root" >&2
        return 0
    fi

    case "$(basename "$staging_root")" in
        mu3library-unity-compile.*)
            if ! removal_error="$(remove_staging_root "$staging_root")"; then
                printf 'warning: could not remove the isolated project; delete it by hand: %s\n' "$staging_root" >&2
                if [[ -n "$removal_error" ]]; then
                    printf 'warning: %s\n' "$removal_error" >&2
                fi
            fi
            ;;
        *)
            printf 'warning: refusing to remove unsafe staging path: %s\n' "$staging_root" >&2
            ;;
    esac

    return 0
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

    target_index "$requested_target" >/dev/null \
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
    local package_relative_path
    local project_path
    local i

    for ((i = 0; i < ${#TARGET_KEYS[@]}; i++)); do
        package_path="${PACKAGE_PATHS[$i]}"
        project_path="${PROJECT_PATHS[$i]}"

        if [[ "$changed_path" == "$package_path" || "$changed_path" == "$package_path/"* ]]; then
            package_relative_path="${changed_path#"$package_path"/}"
            if ! is_package_compile_relevant "$package_relative_path"; then
                return 1
            fi
            printf '%s\n' "${PACKAGE_CONSUMER_TARGETS[$i]}"
            return 0
        fi

        case "$changed_path" in
            "$project_path/Assets/"*|"$project_path/Packages/"*|"$project_path/ProjectSettings/"*)
                printf '%s\n' "${TARGET_KEYS[$i]}"
                return 0
                ;;
        esac
    done

    case "$changed_path" in
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

    load_package_consumers

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

project_path_for_target() {
    local requested_target="$1"
    local index

    index="$(target_index "$requested_target")" || return 1
    printf '%s\n' "${PROJECT_PATHS[$index]}"
}

package_path_for_target() {
    local requested_target="$1"
    local index

    index="$(target_index "$requested_target")" || return 1
    printf '%s\n' "${PACKAGE_PATHS[$index]}"
}

# Unity skips folders whose name ends in ~, so a development project compiles a
# package's samples only through a link from its Assets folder to Samples~.
# Prints why the target's link is unusable and returns 1, or returns 0 when the
# link works or the target configures none.
sample_link_problem() {
    local target_key="$1"
    local index
    local link_path
    local samples_path

    index="$(target_index "$target_key")" || return 0
    [[ -n "${SAMPLE_LINKS[$index]}" ]] || return 0

    link_path="${PROJECT_PATHS[$index]}/${SAMPLE_LINKS[$index]}"
    samples_path="${PACKAGE_PATHS[$index]}/Samples~"

    if [[ ! -d "$REPO_ROOT/$samples_path" ]]; then
        printf '%s does not exist' "$samples_path"
        return 1
    fi
    if [[ ! -e "$REPO_ROOT/$link_path" ]]; then
        printf '%s is missing' "$link_path"
        return 1
    fi
    if [[ ! "$REPO_ROOT/$link_path" -ef "$REPO_ROOT/$samples_path" ]]; then
        printf '%s does not link to %s' "$link_path" "$samples_path"
        return 1
    fi
}

# Reports each selected target whose samples would not compile, as the given
# severity. Returns 1 when one of them should stop the run, which
# --without-samples turns into a warning.
check_sample_links() {
    local severity="$1"
    local target_key
    local problem
    local check_status=0

    # bash before 4.4 treats an empty array as unset under set -u.
    [[ "${#SELECTED_TARGETS[@]}" -gt 0 ]] || return 0

    for target_key in "${SELECTED_TARGETS[@]}"; do
        if problem="$(sample_link_problem "$target_key")"; then
            continue
        fi

        if [[ "$SKIP_SAMPLES" -eq 1 ]]; then
            printf 'warning: %s: %s, so its samples are left out as --without-samples asks.\n' \
                "$target_key" "$problem" >&2
        else
            printf '%s: %s: %s, so its samples would not compile. Run "%s" to link them, or pass --without-samples.\n' \
                "$severity" "$target_key" "$problem" "$LINK_SAMPLES_COMMAND" >&2
            check_status=1
        fi
    done

    return "$check_status"
}

print_selection() {
    local change_entry
    local changed_path
    local mapped_target
    local selected_target

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
        printf '  - %s: %s -> %s\n' \
            "$selected_target" \
            "$(package_path_for_target "$selected_target")" \
            "$(project_path_for_target "$selected_target")"
    done
}

project_editor_version() {
    local target_key="$1"
    local project_path
    local version_file

    project_path="$(project_path_for_target "$target_key")" || return 1
    version_file="$REPO_ROOT/$project_path/ProjectSettings/ProjectVersion.txt"

    [[ -f "$version_file" ]] || return 1
    awk -F': ' '/^m_EditorVersion: / { print $2; exit }' "$version_file"
}

# Unity Hub's install location, which holds one folder per Editor version.
# UNITY_HUB_EDITOR_ROOT replaces it for a Hub set to install Editors elsewhere;
# mu3-cli unity doctor reads the same variable.
hub_editor_root() {
    local editor_root="${UNITY_HUB_EDITOR_ROOT:-}"
    local program_files

    if [[ -n "$editor_root" ]]; then
        if [[ "$editor_root" == "~" || "$editor_root" == "~/"* ]]; then
            editor_root="$HOME${editor_root:1}"
        fi
        if [[ "$(host_platform)" == "windows" ]] && command -v cygpath >/dev/null 2>&1; then
            editor_root="$(cygpath -u "$editor_root")"
        fi
        printf '%s\n' "${editor_root%/}"
        return
    fi

    case "$(host_platform)" in
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
    case "$(host_platform)" in
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

# Trailing part of a Hub installation path, used to tell whether an explicit
# override still points at the Editor version the project asks for.
hub_editor_suffix() {
    local required_version="$1"

    case "$(host_platform)" in
        macos)
            printf '/Editor/%s/Unity.app/Contents/MacOS/Unity\n' "$required_version"
            ;;
        windows)
            printf '/Editor/%s/Editor/Unity.exe\n' "$required_version"
            ;;
        *)
            printf '/Editor/%s/Editor/Unity\n' "$required_version"
            ;;
    esac
}

unity_executable() {
    local target_key="$1"
    local project_path
    local required_version
    local override_path="${UNITY_EDITOR:-${UNITY:-}}"
    local candidate

    project_path="$(project_path_for_target "$target_key")" || return 1
    required_version="$(project_editor_version "$target_key")" || {
        printf 'error: Unity version file not found for %s\n' "$project_path" >&2
        return 1
    }

    if [[ -n "$override_path" ]]; then
        candidate="$override_path"
        if [[ -d "$candidate" && "$candidate" == *.app ]]; then
            candidate="$candidate/Contents/MacOS/Unity"
        fi

        if [[ ! -x "$candidate" ]]; then
            printf 'error: Unity override is not executable: %s\n' "$candidate" >&2
            return 1
        fi

        if [[ "$candidate" != *"$(hub_editor_suffix "$required_version")" ]]; then
            printf 'warning: %s requires Unity %s but the explicit override will be used: %s\n' \
                "$project_path" "$required_version" "$candidate" >&2
        fi
    else
        candidate="$(hub_editor_executable "$required_version")"
        if [[ ! -x "$candidate" ]]; then
            printf 'error: required Unity Editor is not installed for %s: %s\n' \
                "$project_path" "$candidate" >&2
            return 1
        fi
    fi

    printf '%s\n' "$candidate"
}

# Prints held when some process has the file open, free when none does, gone
# when the file no longer exists, or unknown when this host cannot tell.
file_holder_state() {
    local file="$1"
    local quoted_path
    local holders
    local probe_status=0

    case "$(host_platform)" in
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

# Prints absent, open, stale, or unknown for the project's Temp/UnityLockfile.
# A running Editor keeps the file open, so a lock file that no process holds
# was left behind by an Editor that crashed or was killed.
project_lock_state() {
    local target_key="$1"
    local project_path
    local lock_file

    project_path="$(project_path_for_target "$target_key")" || return 1
    lock_file="$REPO_ROOT/$project_path/Temp/UnityLockfile"

    if [[ ! -e "$lock_file" ]]; then
        printf 'absent\n'
        return 0
    fi

    case "$(file_holder_state "$lock_file")" in
        held) printf 'open\n' ;;
        free) printf 'stale\n' ;;
        gone) printf 'absent\n' ;;
        *) printf 'unknown\n' ;;
    esac
}

# Decides where a target runs: sets RUN_LOCATION to in-place or isolated and
# RUN_NOTE to the reason printed with it. Returns 1, with the error in
# RUN_NOTE, when --in-place meets a project that an Editor may hold.
decide_run_location() {
    local target_key="$1"
    local project_path
    local held_reason=""

    project_path="$(project_path_for_target "$target_key")" || return 1
    RUN_LOCATION="in-place"
    RUN_NOTE=""

    case "$(project_lock_state "$target_key")" in
        open)
            held_reason="$project_path is open in Unity"
            ;;
        unknown)
            held_reason="$project_path/Temp/UnityLockfile exists and whether an Editor holds it could not be checked"
            ;;
        stale)
            RUN_NOTE="ignoring a stale $project_path/Temp/UnityLockfile that no running process holds"
            ;;
    esac

    case "$RUN_MODE" in
        isolated)
            RUN_LOCATION="isolated"
            RUN_NOTE="it was requested"
            ;;
        in-place)
            if [[ -n "$held_reason" ]]; then
                RUN_NOTE="$held_reason; close it or omit --in-place to use an isolated mirror"
                return 1
            fi
            ;;
        auto)
            if [[ -n "$held_reason" ]]; then
                RUN_LOCATION="isolated"
                RUN_NOTE="$held_reason"
            fi
            ;;
    esac
}

# Prints where a dry run would run the target.
run_location_phrase() {
    local target_key="$1"

    if ! decide_run_location "$target_key"; then
        printf 'cannot run: %s\n' "$RUN_NOTE"
    elif [[ "$RUN_LOCATION" == "isolated" ]]; then
        printf 'isolated mirror, because %s\n' "$RUN_NOTE"
    elif [[ -n "$RUN_NOTE" ]]; then
        printf 'in place, %s\n' "$RUN_NOTE"
    else
        printf 'in place\n'
    fi
}

# One target's line in the dry-run plan; an entry point that sources this file
# can replace it to add its own conditions.
run_plan_entry() {
    run_location_phrase "$1"
}

print_run_plan() {
    local target_key

    [[ "${#SELECTED_TARGETS[@]}" -gt 0 ]] || return 0

    printf 'Run plan:\n'
    for target_key in "${SELECTED_TARGETS[@]}"; do
        printf '  - %s: %s\n' "$target_key" "$(run_plan_entry "$target_key")"
    done
}

# Copies the contents of a directory, following symlinks so that the samples
# linked into a Unity project become real files in the mirror.
# Git Bash ships no rsync, so cp takes over there.
copy_tree_contents() {
    local source_dir="$1"
    local destination_dir="$2"

    if command -v rsync >/dev/null 2>&1; then
        rsync -aL "$source_dir/" "$destination_dir/"
        return
    fi

    cp -RL "$source_dir/." "$destination_dir/"
}

# Unity clones git-URL package dependencies into Library/PackageCache, which adds
# roughly 130 characters to the project path. On Windows a long staging root
# pushes that clone past MAX_PATH and package resolution fails before compiling.
warn_long_staging_path() {
    local staging_project="$1"
    local windows_path
    local path_length

    [[ "$(host_platform)" == "windows" ]] || return 0

    windows_path="$(to_windows_path "$staging_project")"
    path_length="${#windows_path}"

    if [[ "$path_length" -gt 120 ]]; then
        printf 'warning: staging project path is %d characters, so Unity may fail to clone git package dependencies: %s\n' \
            "$path_length" "$windows_path" >&2
        printf 'warning: set TMPDIR to a shorter directory to stay inside the Windows path limit.\n' >&2
    fi
}

prepare_isolated_project() {
    local target_key="$1"
    local project_path
    local source_project
    local staging_project
    local manifest_file
    local source_dir
    local package_path
    local i

    project_path="$(project_path_for_target "$target_key")" || return 1
    source_project="$REPO_ROOT/$project_path"
    manifest_file="$source_project/Packages/manifest.json"

    STAGING_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/mu3library-unity-compile.XXXXXX")"
    touch "$STAGING_ROOT/.mu3library-unity-compile"

    staging_project="$STAGING_ROOT/$project_path"
    mkdir -p "$staging_project"

    for source_dir in Assets Packages ProjectSettings; do
        [[ -d "$source_project/$source_dir" ]] || fail "missing Unity project directory: $source_project/$source_dir"
        mkdir -p "$staging_project/$source_dir"
        copy_tree_contents "$source_project/$source_dir" "$staging_project/$source_dir"
    done

    [[ -f "$manifest_file" ]] || fail "Unity package manifest not found: $manifest_file"
    for ((i = 0; i < ${#PACKAGE_PATHS[@]}; i++)); do
        package_path="${PACKAGE_PATHS[$i]}"
        if project_references_package "$project_path" "$package_path"; then
            mkdir -p "$STAGING_ROOT/$package_path"
            copy_tree_contents "$REPO_ROOT/$package_path" "$STAGING_ROOT/$package_path"
        fi
    done

    warn_long_staging_path "$staging_project"

    RUN_PROJECT_PATH="$staging_project"
}

# Runs Unity in batch mode for one target, in place or in an isolated mirror,
# with the arguments that follow the verb, and leaves its exit code in
# UNITY_STATUS. The mirror is removed before this returns.
run_unity_for_target() {
    local target_key="$1"
    local verb="$2"
    local unity_path
    local project_path
    local run_project_path

    shift 2
    project_path="$(project_path_for_target "$target_key")" || return 1
    run_project_path="$REPO_ROOT/$project_path"
    unity_path="$(unity_executable "$target_key")"

    if ! decide_run_location "$target_key"; then
        fail "$RUN_NOTE"
    fi

    if [[ "$RUN_LOCATION" == "isolated" ]]; then
        prepare_isolated_project "$target_key"
        run_project_path="$RUN_PROJECT_PATH"
        printf '%s %s (%s) in an isolated mirror because %s.\n' \
            "$verb" "$target_key" "$project_path" "$RUN_NOTE"
    elif [[ -n "$RUN_NOTE" ]]; then
        printf '%s %s (%s) in place, %s.\n' "$verb" "$target_key" "$project_path" "$RUN_NOTE"
    else
        printf '%s %s (%s) in place.\n' "$verb" "$target_key" "$project_path"
    fi

    printf 'Unity: %s\n' "$unity_path"
    printf 'Project: %s\n' "$run_project_path"

    set +e
    "$unity_path" \
        -batchmode \
        -projectPath "$run_project_path" \
        "$@" \
        -logFile -
    UNITY_STATUS=$?
    set -e

    cleanup_staging
}

compile_target() {
    local target_key="$1"

    run_unity_for_target "$target_key" "Compiling" -quit

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
            --isolated)
                RUN_MODE="isolated"
                ;;
            --in-place)
                RUN_MODE="in-place"
                ;;
            --keep-staging)
                KEEP_STAGING=1
                ;;
            --without-samples)
                SKIP_SAMPLES=1
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

# Checks every selected target before Unity starts, so that a missing Editor
# or sample link does not surface only after earlier targets have spent
# minutes running.
preflight_selected_targets() {
    local target_key
    local preflight_failed=0

    for target_key in "${SELECTED_TARGETS[@]}"; do
        if ! unity_executable "$target_key" >/dev/null; then
            preflight_failed=1
        fi
    done

    check_sample_links error || preflight_failed=1

    [[ "$preflight_failed" -eq 0 ]]
}

compile_main() {
    local target_key

    load_package_config
    parse_arguments "$@"
    select_requested_targets
    print_selection

    if [[ "$DRY_RUN" -eq 1 ]]; then
        print_run_plan
        check_sample_links warning || true
        exit 0
    fi

    if [[ "${#SELECTED_TARGETS[@]}" -eq 0 ]]; then
        printf 'No Unity package or development-project changes detected; nothing to compile.\n'
        exit 0
    fi

    trap cleanup_staging EXIT

    preflight_selected_targets || fail "preflight failed; no projects were compiled"

    for target_key in "${SELECTED_TARGETS[@]}"; do
        compile_target "$target_key"
    done
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    compile_main "$@"
fi
