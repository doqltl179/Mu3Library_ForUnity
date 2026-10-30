---
id: cpp
title: C++
kind: language
applies_to: ["**/*.cpp", "**/*.cc", "**/*.cxx", "**/*.hpp", "**/*.hh", "**/*.hxx", "**/*.h", "**/*.ipp", "**/*.inl", "**/*.ixx", "**/*.cppm"]
related: [unreal]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://isocpp.org/std/status", "https://isocpp.github.io/CppCoreGuidelines/CppCoreGuidelines", "https://gcc.gnu.org/projects/cxx-status.html", "https://clang.llvm.org/cxx_status.html", "https://learn.microsoft.com/en-us/cpp/overview/visual-cpp-language-conformance", "https://cmake.org/cmake/help/latest/"]
---

# C++

## Detect
- Unreal project (`*.uproject`, `*.Build.cs`): follow the `unreal` pack; its build tool, macros, and object model replace the defaults here.
- Build system: `CMakeLists.txt` (with `CMakePresets.json`) → CMake; `meson.build` → Meson; `MODULE.bazel`/`WORKSPACE`/`BUILD.bazel` → Bazel; `*.sln`/`*.vcxproj` → MSBuild; otherwise `Makefile`. Use the project's presets or scripts.
- Standard: `CMAKE_CXX_STANDARD` or `target_compile_features(... cxx_std_NN)`, Meson `cpp_std`, MSBuild `<LanguageStandard>`, or `-std=`/`/std:` flags. Use no feature above it.
- Compilers and platforms in the CI matrix (GCC, Clang, MSVC); warning flags and whether warnings are errors.
- Dependencies: `vcpkg.json`, `conanfile.txt`/`conanfile.py`, CMake `FetchContent`, or vendored third-party folders.
- Tooling: `.clang-format`, `.clang-tidy`, `compile_commands.json`, sanitizer presets.

## Conventions
- Own every resource through an object (RAII). Default to `std::unique_ptr`; use `std::shared_ptr` only for shared ownership; create with `std::make_unique`/`std::make_shared`; no owning raw `new`/`delete`.
- Raw pointers and references are non-owning; never let a reference, `std::string_view`, or `std::span` outlive the data it views.
- Follow the rule of zero; a class that manages a resource directly defines or deletes all five special members and marks moves `noexcept`.
- `const` by default; pass cheap types by value, large read-only inputs by `const&`, and sink arguments by value then `std::move`.
- Use `enum class`, `override`, `explicit` single-argument constructors, `nullptr`, and named casts, never C-style casts.
- Headers: include guards or `#pragma once` per project style; include what you use; no `using namespace` at header scope; forward-declare to cut rebuilds.
- Changing a public class layout, inline function, or template changes the ABI; flag it when the library ships as binaries.

## Verify
- Build: `commands.build`, or `cmake --preset <name>` then `cmake --build --preset <name>`; add no new warnings.
- Tests: `commands.test`, or `ctest --preset <name>` / `ctest --test-dir <build-dir> --output-on-failure`.
- Run tests in a sanitizer build when the project has one: AddressSanitizer with UndefinedBehaviorSanitizer (`-fsanitize=address,undefined`); ThreadSanitizer in a separate build for concurrent code.
- When configured: `clang-tidy -p <build-dir> <files>` and `clang-format --dry-run --Werror <files>`.

## Pitfalls
- Undefined behavior compiles silently: signed overflow, out-of-bounds access, uninitialized reads, use-after-free, data races. Initialize every variable and run sanitizers.
- Returning a reference or `string_view` to a local or temporary, or keeping `c_str()` of a temporary, dangles.
- `push_back`, `insert`, `erase`, and rehashing invalidate iterators and references; re-acquire them after mutation.
- `std::move` on a `const` object silently copies; a moved-from object may only be assigned or destroyed.
- Mismatched `new[]`/`delete`; deleting through a base pointer without a virtual destructor.
- ODR violations: one inline function or class defined differently across translation units or build flags.
- Signed/unsigned comparisons and narrowing conversions; heed `-Wall -Wextra` or `/W4`.
- Throwing from a destructor terminates the program, because destructors are `noexcept` by default.

## Version Notes
- Compiler and standard-library support for C++20 modules and C++23 library features (for example `std::expected`, `std::print`) varies by vendor and version; check the support tables before use (as of 2026-10, per gcc.gnu.org, clang.llvm.org, and learn.microsoft.com status pages).
- C++20 named modules need build-system support; CMake supports them from 3.28 with the Ninja and Visual Studio generators (as of 2026-10, per cmake.org).
