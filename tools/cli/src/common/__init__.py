"""Code that the repository's tools share, kept here once rather than copied into each tool.

- checkout: the repository root and the line that names it.
- editor: the Unity Editor a project needs, the Unity package versions it bundles, and the form of a full
  Editor version.
- errors: the one line a tool prints for an exception.
- unity_projects: the project configs in tools/unity-projects and the projects generated from them.
- project_lock: whether an Editor holds a project's Temp/UnityLockfile.
- bash: the bash that runs compile-unity.sh and test-unity.sh.
"""
