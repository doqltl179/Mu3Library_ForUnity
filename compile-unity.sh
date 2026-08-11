#!/usr/bin/env bash

set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly REPO_ROOT="$SCRIPT_DIR"
readonly PACKAGE_CONFIG="$REPO_ROOT/unity-cli-packages.tsv"

TARGET="changed"
TARGET_WAS_SET=0
BASE_REF=""
RUN_MODE="auto"
DRY_RUN=0
KEEP_STAGING=0
STAGING_ROOT=""
RUN_PROJECT_PATH=""

TARGET_KEYS=()
PACKAGE_PATHS=()
PROJECT_PATHS=()
CHANGED_FILES=()
RELEVANT_CHANGES=()
IGNORED_CHANGES=()
SELECTED_TARGETS=()

fail() {
    printf 'error: %s\n' "$*" >&2
    exit 1
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
    local extra

    [[ -f "$PACKAGE_CONFIG" ]] || fail "package mapping not found: $PACKAGE_CONFIG"

    while IFS=$'\t' read -r target_key package_path project_path extra || [[ -n "$target_key$package_path$project_path$extra" ]]; do
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
        if target_index "$target_key" >/dev/null; then
            fail "duplicate target '$target_key' at $PACKAGE_CONFIG:$line_number"
        fi
        [[ -d "$REPO_ROOT/$package_path" ]] || fail "package directory not found: $REPO_ROOT/$package_path"
        [[ -d "$REPO_ROOT/$project_path" ]] || fail "Unity project directory not found: $REPO_ROOT/$project_path"

        TARGET_KEYS+=("$target_key")
        PACKAGE_PATHS+=("$package_path")
        PROJECT_PATHS+=("$project_path")
    done < "$PACKAGE_CONFIG"

    [[ "${#TARGET_KEYS[@]}" -gt 0 ]] || fail "no package mappings found in $PACKAGE_CONFIG"
}

usage() {
    local i

    cat <<'EOF'
Usage: ./compile-unity.sh [changed|all|target] [options]

Selection:
  changed          Compile only packages affected by Git changes (default).
  all              Compile every configured package project.
EOF

    for ((i = 0; i < ${#TARGET_KEYS[@]}; i++)); do
        printf '  %-16s Compile %s for %s\n' \
            "${TARGET_KEYS[$i]}" "${PACKAGE_PATHS[$i]}" "${PROJECT_PATHS[$i]}"
    done

    cat <<'EOF'

Options:
  --base <git-ref>  Include committed changes from <git-ref>...HEAD.
  --dry-run         Print changed files and selected targets without Unity.
  --isolated        Always compile a temporary source-only project mirror.
  --in-place        Compile the repository project directly; fail if it is open.
  --keep-staging    Keep an isolated project after the run for diagnostics.
  -h, --help        Show this help.

Environment:
  UNITY_EDITOR or UNITY
      Optional path to a Unity executable (or Unity.app) that overrides the
      project version lookup. Without an override, the script reads each
      ProjectVersion.txt and uses the matching Unity Hub installation.

Changed mode includes staged, unstaged, and untracked files. With --base it
also includes committed changes from the requested ref through HEAD. Package
ownership and development projects are configured in unity-cli-packages.tsv.

The default execution mode compiles in place when a project is closed. If the
same project is open in Unity, it compiles a temporary mirror instead, leaving
the running editor, repository packages, and repository Library untouched.
EOF
}

cleanup_staging() {
    local staging_root="$STAGING_ROOT"
    STAGING_ROOT=""
    RUN_PROJECT_PATH=""

    if [[ -z "$staging_root" ]]; then
        return
    fi

    if [[ "$KEEP_STAGING" -eq 1 ]]; then
        printf 'Kept isolated project: %s\n' "$staging_root"
        return
    fi

    if [[ ! -f "$staging_root/.mu3library-unity-compile" ]]; then
        printf 'warning: refusing to remove unrecognized staging path: %s\n' "$staging_root" >&2
        return
    fi

    case "$(basename "$staging_root")" in
        mu3library-unity-compile.*)
            rm -rf -- "$staging_root"
            ;;
        *)
            printf 'warning: refusing to remove unsafe staging path: %s\n' "$staging_root" >&2
            ;;
    esac
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
        || fail "unknown compile target: $requested_target"

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

target_for_changed_path() {
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
            printf '%s\n' "${TARGET_KEYS[$i]}"
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
        tools/*|docs/*|tasks/*|.github/*)
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
    local mapped_target

    collect_changed_files

    if [[ "${#CHANGED_FILES[@]}" -eq 0 ]]; then
        return
    fi

    for changed_file in "${CHANGED_FILES[@]}"; do
        if mapped_target="$(target_for_changed_path "$changed_file")"; then
            RELEVANT_CHANGES+=("$changed_file"$'\t'"$mapped_target")
            if [[ "$mapped_target" == "all" ]]; then
                select_all_targets
            else
                select_target "$mapped_target"
            fi
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
        printf 'Selected compile targets: none\n'
        return
    fi

    printf 'Selected compile targets:\n'
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

        if [[ "$candidate" != *"/Editor/$required_version/Unity.app/Contents/MacOS/Unity" ]]; then
            printf 'warning: %s requires Unity %s but the explicit override will be used: %s\n' \
                "$project_path" "$required_version" "$candidate" >&2
        fi
    else
        candidate="/Applications/Unity/Hub/Editor/$required_version/Unity.app/Contents/MacOS/Unity"
        if [[ ! -x "$candidate" ]]; then
            printf 'error: required Unity Editor is not installed for %s: %s\n' \
                "$project_path" "$candidate" >&2
            return 1
        fi
    fi

    printf '%s\n' "$candidate"
}

project_is_open() {
    local target_key="$1"
    local project_path

    project_path="$(project_path_for_target "$target_key")" || return 1
    [[ -e "$REPO_ROOT/$project_path/Temp/UnityLockfile" ]]
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
        rsync -aL "$source_project/$source_dir/" "$staging_project/$source_dir/"
    done

    [[ -f "$manifest_file" ]] || fail "Unity package manifest not found: $manifest_file"
    for ((i = 0; i < ${#PACKAGE_PATHS[@]}; i++)); do
        package_path="${PACKAGE_PATHS[$i]}"
        if grep -Fq "file:../../$package_path" "$manifest_file"; then
            mkdir -p "$STAGING_ROOT/$package_path"
            rsync -aL "$REPO_ROOT/$package_path/" "$STAGING_ROOT/$package_path/"
        fi
    done

    RUN_PROJECT_PATH="$staging_project"
}

compile_target() {
    local target_key="$1"
    local unity_path
    local project_path
    local run_project_path
    local use_isolated=0
    local isolated_reason
    local unity_status

    project_path="$(project_path_for_target "$target_key")" || return 1
    run_project_path="$REPO_ROOT/$project_path"
    unity_path="$(unity_executable "$target_key")"

    case "$RUN_MODE" in
        isolated)
            use_isolated=1
            ;;
        in-place)
            if project_is_open "$target_key"; then
                fail "$project_path is open in Unity; close it or omit --in-place to use an isolated mirror"
            fi
            ;;
        auto)
            if project_is_open "$target_key"; then
                use_isolated=1
            fi
            ;;
    esac

    if [[ "$use_isolated" -eq 1 ]]; then
        if [[ "$RUN_MODE" == "isolated" ]]; then
            isolated_reason="it was requested"
        else
            isolated_reason="the project is open"
        fi

        prepare_isolated_project "$target_key"
        run_project_path="$RUN_PROJECT_PATH"
        printf 'Compiling %s (%s) in an isolated mirror because %s.\n' \
            "$target_key" "$project_path" "$isolated_reason"
    else
        printf 'Compiling %s (%s) in place.\n' "$target_key" "$project_path"
    fi

    printf 'Unity: %s\n' "$unity_path"
    printf 'Project: %s\n' "$run_project_path"

    set +e
    "$unity_path" \
        -batchmode \
        -quit \
        -projectPath "$run_project_path" \
        -logFile -
    unity_status=$?
    set -e

    cleanup_staging

    if [[ "$unity_status" -ne 0 ]]; then
        printf 'error: Unity compilation failed for %s (exit %d)\n' "$target_key" "$unity_status" >&2
        return "$unity_status"
    fi

    printf 'Unity compilation succeeded: %s\n' "$target_key"
}

load_package_config

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
        -h|--help)
            usage
            exit 0
            ;;
        --*)
            fail "unknown option: $1"
            ;;
        *)
            [[ "$TARGET_WAS_SET" -eq 0 ]] || fail "only one compile target may be specified"
            TARGET="$1"
            TARGET_WAS_SET=1
            ;;
    esac
    shift
done

if [[ -n "$BASE_REF" && "$TARGET" != "changed" ]]; then
    fail "--base can only be used with the changed target"
fi

if [[ "$TARGET" == "changed" ]]; then
    select_changed_targets
elif [[ "$TARGET" == "all" ]]; then
    select_all_targets
else
    select_target "$TARGET"
fi

print_selection

if [[ "$DRY_RUN" -eq 1 ]]; then
    exit 0
fi

if [[ "${#SELECTED_TARGETS[@]}" -eq 0 ]]; then
    printf 'No Unity package or development-project changes detected; nothing to compile.\n'
    exit 0
fi

trap cleanup_staging EXIT

preflight_failed=0
for target_key in "${SELECTED_TARGETS[@]}"; do
    if ! unity_executable "$target_key" >/dev/null; then
        preflight_failed=1
    fi
done

if [[ "$preflight_failed" -ne 0 ]]; then
    fail "Unity Editor preflight failed; no projects were compiled"
fi

for target_key in "${SELECTED_TARGETS[@]}"; do
    compile_target "$target_key"
done
