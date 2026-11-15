from __future__ import annotations

import platform
import shutil
import subprocess
from pathlib import Path
from typing import Callable


_WINDOWS_GENERIC_READ = 0x80000000
_WINDOWS_OPEN_EXISTING = 3
_WINDOWS_FILE_ATTRIBUTE_NORMAL = 0x80
_WINDOWS_ERROR_FILE_NOT_FOUND = 2
_WINDOWS_ERROR_PATH_NOT_FOUND = 3
_WINDOWS_ERROR_SHARING_VIOLATION = 32


def _windows_file_is_held_open(path: Path) -> bool | None:
    import ctypes
    from ctypes import wintypes

    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    create_file = kernel32.CreateFileW
    create_file.argtypes = (
        wintypes.LPCWSTR,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.LPVOID,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.HANDLE,
    )
    create_file.restype = wintypes.HANDLE
    close_handle = kernel32.CloseHandle
    close_handle.argtypes = (wintypes.HANDLE,)
    close_handle.restype = wintypes.BOOL

    # Opening the file without sharing fails with a sharing violation for as long
    # as any other handle is open, whatever access that handle asked for.
    handle = create_file(
        str(path), _WINDOWS_GENERIC_READ, 0, None, _WINDOWS_OPEN_EXISTING, _WINDOWS_FILE_ATTRIBUTE_NORMAL, None
    )
    if handle is None or handle == ctypes.c_void_p(-1).value:
        error = ctypes.get_last_error()
        if error == _WINDOWS_ERROR_SHARING_VIOLATION:
            return True
        if error in (_WINDOWS_ERROR_FILE_NOT_FOUND, _WINDOWS_ERROR_PATH_NOT_FOUND):
            return False
        return None

    close_handle(handle)
    return False


def file_is_held_open(path: Path) -> bool | None:
    """Return whether a process holds the file open, or None when this host cannot tell.

    compile-unity.sh answers the same question with PowerShell on Windows and lsof elsewhere.
    """
    if platform.system() == "Windows":
        return _windows_file_is_held_open(path)

    lsof = shutil.which("lsof")
    if lsof is None:
        return None
    # lsof prints the holders' process IDs and exits 1 when there are none.
    completed = subprocess.run([lsof, "-t", "--", str(path)], capture_output=True, text=True, check=False)
    return bool(completed.stdout.strip())


def project_lock_state(project_root: Path, probe: Callable[[Path], bool | None] = file_is_held_open) -> str:
    """Return absent, open, stale, or unknown for the project's Temp/UnityLockfile.

    A running Editor keeps the file open, so a lock file that no process holds was left
    behind by an Editor that crashed or was killed and does not mean the project is open.
    """
    lock_file = project_root / "Temp" / "UnityLockfile"
    if not lock_file.exists():
        return "absent"

    held = probe(lock_file)
    if held is None:
        return "unknown"
    if held:
        return "open"
    return "stale" if lock_file.exists() else "absent"
