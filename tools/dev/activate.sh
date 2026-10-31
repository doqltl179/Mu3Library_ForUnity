# shellcheck shell=bash
# Enters the repository's development environment in this bash session:
#   - creates .venv at the repository root on first use and installs mu3-cli into it,
#   - sets the variables in .env.local (see tools/dev/env.local.example),
#   - activates that virtual environment.
#
# From the repository root:   source tools/dev/activate.sh
# (from elsewhere, give the script's path)
# Leave it again with:        deactivate
#
# It runs inside the caller's shell, so it sets no shell options (no `set -euo pipefail`): those
# would stay on in the interactive shell after it returns. Its locals start with _mu3_ so that a
# variable in .env.local cannot share a name with one and be set only inside the function.

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
    printf 'Source this script so it can change the shell: source %s\n' "$0" >&2
    exit 1
fi

if [[ -n "${_MU3_DEV_ACTIVE:-}" ]] && declare -F deactivate >/dev/null; then
    deactivate
fi

_mu3_dev_enter() {
    local _mu3_root _mu3_venv _mu3_scripts _mu3_cli _mu3_python
    # An empty CDPATH keeps cd from printing the folder into the result.
    _mu3_root="$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)" || return 1
    _mu3_venv="$_mu3_root/.venv"
    if [[ -d "$_mu3_venv/Scripts" || ( ! -d "$_mu3_venv/bin" && "${OS:-}" == "Windows_NT" ) ]]; then
        _mu3_scripts="$_mu3_venv/Scripts"
    else
        _mu3_scripts="$_mu3_venv/bin"
    fi

    # --- Virtual environment --------------------------------------------------------------
    if [[ ! -f "$_mu3_scripts/activate" ]]; then
        printf '[mu3] Creating %s and installing mu3-cli...\n' "$_mu3_venv"
        _mu3_cli="$_mu3_root/tools/cli"
        _mu3_python="$(command -v python || command -v python3)"
        if command -v uv >/dev/null 2>&1; then
            uv venv "$_mu3_venv" --prompt mu3 --quiet \
                && uv pip install --python "$_mu3_venv" --quiet -e "$_mu3_cli"
        elif [[ -n "$_mu3_python" ]]; then
            "$_mu3_python" -m venv "$_mu3_venv" --prompt mu3 \
                && "$_mu3_scripts/python" -m pip install --quiet -e "$_mu3_cli"
        else
            printf '[mu3] Neither uv nor Python was found; install one of them and try again.\n' >&2
            return 1
        fi || {
            printf '[mu3] Setting up %s failed; nothing was activated. Delete the folder and try again.\n' "$_mu3_venv" >&2
            return 1
        }
    fi

    # --- What deactivate restores ---------------------------------------------------------
    _MU3_DEV_SAVED_NAMES=(MU3_DEV_ROOT)
    _MU3_DEV_SAVED_VALUES=("${MU3_DEV_ROOT-}")
    _MU3_DEV_SAVED_SET=("${MU3_DEV_ROOT+1}")

    # --- .env.local -----------------------------------------------------------------------
    # Loaded before the virtual environment, so a variable that changes how it activates, such
    # as VIRTUAL_ENV_DISABLE_PROMPT, takes effect.
    local _mu3_env_file="$_mu3_root/.env.local" _mu3_line _mu3_text _mu3_key _mu3_value _mu3_number=0 _mu3_loaded=()
    if [[ -f "$_mu3_env_file" ]]; then
        while IFS= read -r _mu3_line || [[ -n "$_mu3_line" ]]; do
            _mu3_number=$((_mu3_number + 1))
            _mu3_text="${_mu3_line%$'\r'}"
            # Windows editors and PowerShell's UTF8 encoding start the file with a byte order mark.
            [[ $_mu3_number -eq 1 ]] && _mu3_text="${_mu3_text#$'\xef\xbb\xbf'}"
            _mu3_text="${_mu3_text#"${_mu3_text%%[![:space:]]*}"}"
            _mu3_text="${_mu3_text%"${_mu3_text##*[![:space:]]}"}"
            [[ -z "$_mu3_text" || "$_mu3_text" == \#* ]] && continue

            if [[ ! "$_mu3_text" =~ ^(export[[:space:]]+)?([A-Za-z_][A-Za-z0-9_]*)[[:space:]]*=[[:space:]]*(.*)$ ]]; then
                printf '[mu3] .env.local line %d is not KEY=VALUE; skipped.\n' "$_mu3_number" >&2
                continue
            fi

            _mu3_key="${BASH_REMATCH[2]}"
            _mu3_value="${BASH_REMATCH[3]}"
            if [[ ${#_mu3_value} -ge 2 && ( "$_mu3_value" == \"*\" || "$_mu3_value" == \'*\' ) ]]; then
                _mu3_value="${_mu3_value:1:${#_mu3_value}-2}"
            fi

            _MU3_DEV_SAVED_NAMES+=("$_mu3_key")
            _MU3_DEV_SAVED_VALUES+=("${!_mu3_key-}")
            if [[ -n "${!_mu3_key+1}" ]]; then _MU3_DEV_SAVED_SET+=(1); else _MU3_DEV_SAVED_SET+=(""); fi

            export "$_mu3_key=$_mu3_value"
            _mu3_loaded+=("$_mu3_key")
        done < "$_mu3_env_file"
    fi

    # The virtual environment's own deactivate is kept under another name, and the one defined
    # below undoes both.
    # shellcheck disable=SC1091
    source "$_mu3_scripts/activate"
    eval "$(declare -f deactivate | sed '1s/deactivate/_mu3_dev_venv_deactivate/')"

    deactivate() {
        if declare -F _mu3_dev_venv_deactivate >/dev/null; then
            _mu3_dev_venv_deactivate "$@"
            unset -f _mu3_dev_venv_deactivate
        fi

        # Restored from the last saved entry backwards, so a variable listed twice ends at its first value.
        local _mu3_i
        for ((_mu3_i = ${#_MU3_DEV_SAVED_NAMES[@]} - 1; _mu3_i >= 0; _mu3_i--)); do
            if [[ -n "${_MU3_DEV_SAVED_SET[_mu3_i]}" ]]; then
                export "${_MU3_DEV_SAVED_NAMES[_mu3_i]}=${_MU3_DEV_SAVED_VALUES[_mu3_i]}"
            else
                unset "${_MU3_DEV_SAVED_NAMES[_mu3_i]}"
            fi
        done

        unset _MU3_DEV_SAVED_NAMES _MU3_DEV_SAVED_VALUES _MU3_DEV_SAVED_SET _MU3_DEV_ACTIVE
        if [[ "${1:-}" != "nondestructive" ]]; then
            unset -f deactivate
        fi
    }

    export MU3_DEV_ROOT="$_mu3_root"
    _MU3_DEV_ACTIVE=1

    printf '[mu3] Development environment for %s\n' "$_mu3_root"
    printf '[mu3]   python : %s\n' "$(command -v python)"
    printf '[mu3]   bash   : %s\n' "$(command -v bash)"
    if [[ ${#_mu3_loaded[@]} -gt 0 ]]; then
        printf '[mu3]   .env.local: %s\n' "${_mu3_loaded[*]}"
    else
        printf '[mu3]   .env.local: no variables\n'
    fi
    printf '[mu3] Leave with: deactivate\n'
}

if _mu3_dev_enter; then
    unset -f _mu3_dev_enter
else
    unset -f _mu3_dev_enter
    return 1
fi
