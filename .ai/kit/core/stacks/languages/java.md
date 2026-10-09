---
id: java
title: Java
kind: language
applies_to: ["**/*.java"]
related: [spring-boot, kotlin]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://docs.oracle.com/en/java/javase/", "https://openjdk.org/projects/jdk/", "https://www.oracle.com/java/technologies/java-se-support-roadmap.html", "https://docs.gradle.org/current/userguide/toolchains.html", "https://maven.apache.org/guides/"]
---

# Java

## Detect
- Build tool: `pom.xml` → Maven; `build.gradle(.kts)` and `settings.gradle(.kts)` → Gradle. Use the committed wrapper (`./mvnw`, `./gradlew`; `.cmd` on Windows), not a global install.
- Java version: Gradle `java.toolchain.languageVersion` or `jvmToolchain(N)`; Maven `maven.compiler.release` or the compiler plugin's `<release>`; also `.java-version`, `.sdkmanrc`, `.tool-versions`. Use no syntax or API above it.
- Modules: Maven `<modules>`, Gradle `include(...)` in settings; dependency versions in a parent POM, BOM, or `gradle/libs.versions.toml`.
- Test stack: JUnit 5 (`org.junit.jupiter`), JUnit 4 (`org.junit.Test`), or TestNG; the mocking and assertion libraries in use.
- Code generators and checkers: Lombok, MapStruct, Error Prone, NullAway, Checkstyle, Spotless; the nullability annotations in use (for example JSpecify `@Nullable`).

## Conventions
- When the configured release supports them, use records for immutable data carriers, sealed interfaces with pattern-matching `switch` for closed hierarchies, and text blocks for multi-line strings.
- Follow the project's nullability convention and annotate new public API with it. Return `Optional` for absent results; never use it for fields or parameters.
- Close every `AutoCloseable` with try-with-resources.
- Prefer immutable collections (`List.of`, `Map.of`) and `final` fields.
- Wrap exceptions with their cause (`new XException("msg", e)`); never swallow, and never both log and rethrow the same exception.
- Use `java.time` (`Instant`, `LocalDate`, `ZonedDateTime`), not `Date`/`Calendar`.
- Override `equals` and `hashCode` together.
- Log with parameterized messages (`log.info("id={}", id)`), not string concatenation.
- Add dependencies where the module already declares them (build file or version catalog).

## Verify
- Build with all checks: `commands.build`, or `./gradlew build` / `./mvnw verify`.
- Tests: `commands.test`; narrow with `./gradlew :module:test --tests 'pkg.ClassTest'` or `./mvnw -pl module -am -Dtest=ClassTest -Dsurefire.failIfNoSpecifiedTests=false test`.
- Lint and format: `commands.lint`, `commands.format` (for example `spotlessCheck`, `checkstyleMain`).

## Pitfalls
- `==` on `String` or boxed numbers compares references; use `equals` (boxed-integer caching makes small values appear to work).
- Modifying a collection while iterating it throws `ConcurrentModificationException`; use `Iterator.remove` or `removeIf`.
- `Optional.get()` without a check; use `orElse`, `orElseThrow`, or `map`.
- Shared `SimpleDateFormat` or other non-thread-safe objects in static fields; use `DateTimeFormatter`.
- Catching `InterruptedException` and continuing; restore the flag with `Thread.currentThread().interrupt()` or propagate it.
- Editing generated sources under `target/` or `build/`; change the annotation, schema, or template and rebuild.
- Using preview features; they need `--enable-preview` and change between releases.

## Version Notes
- Feature releases ship every six months; recent LTS releases are 11, 17, 21, and 25 (as of 2026-10, per oracle.com Java SE support roadmap).
- Minimum release per feature: text blocks 15; records 16; sealed classes 17; pattern-matching `switch`, record patterns, virtual threads, and sequenced collections 21 (as of 2026-10, per openjdk.org JEPs).
- On JDK 21–23, blocking inside `synchronized` pins a virtual thread to its carrier; JDK 24+ removes this (JEP 491). Prefer `ReentrantLock` around blocking calls on older releases (as of 2026-10, per openjdk.org).
