from __future__ import annotations


def describe_error(error: BaseException) -> str:
    """One line naming an exception and its message, such as PermissionError: [WinError 5] ..."""
    message = " ".join(str(error).split())
    return f"{type(error).__name__}: {message}" if message else type(error).__name__
