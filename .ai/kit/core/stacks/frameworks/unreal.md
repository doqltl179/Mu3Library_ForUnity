---
id: unreal
title: Unreal Engine
kind: framework
applies_to: ["**/Source/**/*.cpp", "**/Source/**/*.h", "**/*.Build.cs", "**/*.Target.cs", "**/*.uproject", "**/*.uplugin"]
related: [cpp]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://dev.epicgames.com/documentation/en-us/unreal-engine", "https://dev.epicgames.com/documentation/en-us/unreal-engine/epic-cplusplus-coding-standard-for-unreal-engine", "https://dev.epicgames.com/documentation/en-us/unreal-engine/using-live-coding-to-recompile-unreal-engine-applications-at-runtime", "https://dev.epicgames.com/documentation/unreal-engine/whats-new"]
---

# Unreal Engine

## Detect
- Engine version: `EngineAssociation` in `<Project>.uproject`, either a version such as `5.4` or a GUID/empty value for a source-built engine registered on the machine. Use that engine for every build.
- Modules: `Modules` in `.uproject`/`.uplugin` (name, `Type` such as `Runtime` or `Editor`, `LoadingPhase`); dependencies in `Source/<Module>/<Module>.Build.cs`; targets in `Source/*.Target.cs`.
- Plugins: `Plugins/<Name>/<Name>.uplugin` and the `Plugins` list in `.uproject`.
- Config: `Config/Default*.ini` (engine, game, input, `[CoreRedirects]`).
- No `Source/` folder means a Blueprint-only project; adding C++ requires a new module and regenerated project files.

## Conventions
- Reflect types with `UCLASS()`, `USTRUCT()`, `UENUM()`, `UINTERFACE()`, and members with `UPROPERTY()`/`UFUNCTION()`. Each reflected class or struct body starts with `GENERATED_BODY()`, and `#include "<File>.generated.h"` is the last include in its header.
- Use Unreal prefixes: `U` (UObject), `A` (AActor), `F` (structs and plain types), `E` (enums), `I` (interfaces), `T` (templates), `b` (bool members). Unreal Header Tool rejects wrong prefixes on reflected types.
- Hold every `UObject` reference that must keep its target alive in a `UPROPERTY()` member; non-UObject holders report references through `FGCObject::AddReferencedObjects`; non-owning references use `TWeakObjectPtr`. Unmarked raw pointers dangle after garbage collection.
- Create objects with `NewObject<T>()`, `GetWorld()->SpawnActor<T>()`, or `CreateDefaultSubobject<T>()` (constructors only); never `new`/`delete` a `UObject`.
- Constructors build the class default object: set defaults and components there; put gameplay logic in `BeginPlay` or later.
- Check objects with `IsValid(Obj)`, which also rejects objects pending destruction.
- Add a module to `PublicDependencyModuleNames` when its types appear in your public headers, otherwise to `PrivateDependencyModuleNames`; export types used by other modules with `<MODULE>_API`.
- Include what you use and forward-declare in headers; never include monolithic headers such as `Engine.h`.
- Use engine types instead of STL: `TArray`, `TMap`, `TSet`, `FString`, `FName` (identifiers), `FText` (user-facing, localizable), `TEXT("...")` literals.
- Expose to Blueprints deliberately: `BlueprintCallable`, `BlueprintPure`, `BlueprintImplementableEvent`, `BlueprintNativeEvent` (implement `<Name>_Implementation`), and property specifiers (`EditAnywhere`, `VisibleAnywhere`, `BlueprintReadOnly`, `BlueprintReadWrite`). Keep core and performance-critical logic in C++; Blueprints configure and compose.
- When renaming or removing a reflected class, property, function, or enum that assets use, add a `[CoreRedirects]` entry in `Config/DefaultEngine.ini` (`+ClassRedirects`, `+PropertyRedirects`, `+FunctionRedirects`, `+EnumRedirects`) so saved assets still load.
- Put editor-only code in modules of `Type` `Editor` or behind `#if WITH_EDITOR` (`WITH_EDITORONLY_DATA` for members); a runtime module depending on `UnrealEd` breaks packaged builds.
- Disable ticking (`PrimaryActorTick.bCanEverTick = false`) on actors and components that do not need it.

## Verify
- Prefer `commands.build` and `commands.test`.
- Build the editor target with UnrealBuildTool: `Engine/Build/BatchFiles/Build.bat <Project>Editor Win64 Development -Project="<path>.uproject" -WaitMutex` (`Engine/Build/BatchFiles/Mac/Build.sh` or `Linux/Build.sh` elsewhere). Close the Editor first; the build fails while Live Coding is active.
- Cook and package: `Engine/Build/BatchFiles/RunUAT.bat BuildCookRun -project="<path>.uproject" -platform=<Platform> -clientconfig=Development -build -cook -stage -pak` (`RunUAT.sh` on Mac and Linux).
- Automation tests: `UnrealEditor-Cmd "<path>.uproject" -ExecCmds="Automation RunTests <Filter>; Quit" -unattended -nopause -nullrhi -log`; drop `-nullrhi` for tests that need rendering; list test names with `Automation List`.

## Pitfalls
- Editing `.uasset`/`.umap` as text: they are binary; change them in the Editor or through editor scripting.
- Committing `Binaries/`, `Intermediate/`, `Saved/`, `DerivedDataCache/`, or generated IDE project files: they are regenerated.
- Relying on Live Coding after header, reflection, or constructor changes: constructor defaults do not update existing instances and large structural changes can crash. Close the Editor and do a full build. Do not use Hot Reload.
- `unresolved external symbol` link errors usually mean a missing module dependency in `.Build.cs` or a missing `<MODULE>_API` export.
- `check()` is compiled out of Shipping builds; never put required side effects in it (use `verify()`).
- Calling `UObject` or engine APIs off the game thread; marshal results back to the game thread first.

## Version Notes
- UE5 binaries are `UnrealEditor`/`UnrealEditor-Cmd`; UE4 uses `UE4Editor`/`UE4Editor-Cmd` (as of 2026-10, per Epic docs).
- `TObjectPtr<T>` is the recommended type for `UPROPERTY` object pointer members in UE5 (as of 2026-10, per Epic docs).
- Live Coding with Object Reinstancing is the default in UE5; Hot Reload is used only when Live Coding is disabled (as of 2026-10, per Epic Live Coding docs).
