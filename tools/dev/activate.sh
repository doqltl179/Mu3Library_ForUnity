# shellcheck shell=bash
# Enters the repository's development environment in this bash session:
#   - creates .venv at the repository root on first use and installs mu3-cli into it,
#   - activates that virtual environment,
#   - sets the variables in .env.local (see tools/dev/env.local.example).
#
# Source it, from any folder:   source tools/dev/activate.sh
# Leave it again with:          deactivate
#
# It runs inside the caller's shell, so it sets no shell options (no `set -euo pipefail`): those
# would stay on in the interactive shell after it returns.

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    printf 'Source this script so it can change the shell: source %s\n' "$0" >&2
    exit 1
fi

if [[ -n "${_MU3_DEV_ACTIVE:-}" ]] && declare -F deactivate >/dev/null; then
    deactivate
fi

_mu3_dev_enter() {
    local root venv scripts cli
    root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)" || return 1
    venv="$root/.venv"
    if [[ -d "$venv/Scripts" || ( ! -d "$venv/bin" && "${OS:-}" == "Windows_NT" ) ]]; then
        scripts="$venv/Scripts"
    else
        scripts="$venv/bin"
    fi

    # --- Virtual environment --------------------------------------------------------------
    if [[ ! -f "$scripts/activate" ]]; then
        printf '[mu3] Creating %s and installing mu3-cli...\n' "$venv"
        cli="$root/tools/cli"
        if command -v uv >/dev/null 2>&1; then
            uv venv "$venv" --prompt mu3 --quiet \
                && uv pip install --python "$venv" --quiet -e "$cli" \
                || { printf '[mu3] Setting up %s failed; nothing was activated. Delete the folder and try again.\n' "$venv" >&2; return 1; }
        else
            python -m venv "$venv" --prompt mu3 \
                && "$scripts/python" -m pip install --quiet -e "$cli" \
                || { printf '[mu3] Setting up %s failed; nothing was activated. Delete the folder and try again.\n' "$venv" >&2; return 1; }
        fi
    fi

    # --- What deactivate restores ---------------------------------------------------------
    _MU3_DEV_SAVED_NAMES=(MU3_DEV_ROOT)
    _MU3_DEV_SAVED_VALUES=("${MU3_DEV_ROOT-}")
    _MU3_DEV_SAVED_SET=("${MU3_DEV_ROOT+1}")

    # The virtual environment's own deactivate is kept under another name, and the one defined
    # below undoes both.
    # shellcheck disable=SC1091
    source "$scripts/activate"
    eval "$(declare -f deactivate | sed '1s/deactivate/_mu3_dev_venv_deactivate/')"

    deactivate() {
        if declare -F _mu3_dev_venv_deactivate >/dev/null; then
            _mu3_dev_venv_deactivate "$@"
            unset -f _mu3_dev_venv_deactivate
        fi

        # Restored from the last saved entry backwards, so a variable listed twice ends at its first value.
        local i
        for ((i = ${#_MU3_DEV_SAVED_NAMES[@]} - 1; i >= 0; i--)); do
            if [[ -n "${_MU3_DEV_SAVED_SET[i]}" ]]; then
                export "${_MU3_DEV_SAVED_NAMES[i]}=${_MU3_DEV_SAVED_VALUES[i]}"
            else
                unset "${_MU3_DEV_SAVED_NAMES[i]}"
            fi
        done

        unset _MU3_DEV_SAVED_NAMES _MU3_DEV_SAVED_VALUES _MU3_DEV_SAVED_SET _MU3_DEV_ACTIVE
        if [[ "${1:-}" != "nondestructive" ]]; then
            unset -f deactivate
        fi
    }

    # --- .env.local -----------------------------------------------------------------------
    local env_file="$root/.env.local" line text key value number=0 loaded=()
    if [[ -f "$env_file" ]]; then
        while IFS= read -r line || [[ -n "$line" ]]; do
            number=$((number + 1))
            text="${line%$'\r'}"
            # Windows editors and PowerShell's UTF8 encoding start the file with a byte order mark.
            [[ $number -eq 1 ]] && text="${text#$'\xef\xbb\xbf'}"
            text="${text#"${text%%[![:space:]]*}"}"
            text="${text%"${text##*[![:space:]]}"}"
            [[ -z "$text" || "$text" == \#* ]] && continue

            if [[ ! "$text" =~ ^(export[[:space:]]+)?([A-Za-z_][A-Za-z0-9_]*)[[:space:]]*=[[:space:]]*(.*)$ ]]; then
                printf '[mu3] .env.local line %d is not KEY=VALUE; skipped.\n' "$number" >&2
                continue
            fi

            key="${BASH_REMATCH[2]}"
            value="${BASH_REMATCH[3]}"
            if [[ ${#value} -ge 2 && ( "$value" == \"*\" || "$value" == \'*\' ) ]]; then
                value="${value:1:${#value}-2}"
            fi

            _MU3_DEV_SAVED_NAMES+=("$key")
            _MU3_DEV_SAVED_VALUES+=("${!key-}")
            if [[ -n "${!key+1}" ]]; then _MU3_DEV_SAVED_SET+=(1); else _MU3_DEV_SAVED_SET+=(""); fi

            export "$key=$value"
            loaded+=("$key")
        done < "$env_file"
    fi

    export MU3_DEV_ROOT="$root"
    _MU3_DEV_ACTIVE=1

    printf '[mu3] Development environment for %s\n' "$root"
    printf '[mu3]   python : %s\n' "$(command -v python)"
    printf '[mu3]   bash   : %s\n' "$(command -v bash)"
    if [[ ${#loaded[@]} -gt 0 ]]; then
        printf '[mu3]   .env.local: %s\n' "${loaded[*]}"
    else
        printf '[mu3]   .env.local: no variables\n'
    fi
    printf '[mu3] Leave with: deactivate\n'
}

_mu3_dev_enter
unset -f _mu3_dev_enter
