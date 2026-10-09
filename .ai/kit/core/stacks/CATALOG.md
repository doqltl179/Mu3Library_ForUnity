<!-- agentkit:generated from core/stacks/. Do not edit: change the source, then run `python tools/agentkit.py sync`. Paths are relative to the project root. -->

# Stack Pack Catalog

Every core stack pack, by kind.

## language

| Pack | Default paths | Related |
|---|---|---|
| [`cpp`](languages/cpp.md) C++ | `**/*.cpp`, `**/*.cc`, `**/*.cxx`, `**/*.hpp`, `**/*.hh`, `**/*.hxx`, `**/*.h`, `**/*.ipp`, `**/*.inl`, `**/*.ixx`, `**/*.cppm` | unreal |
| [`csharp`](languages/csharp.md) C# | `**/*.cs` | aspnet-core, unity |
| [`dart`](languages/dart.md) Dart | `**/*.dart` | flutter |
| [`go`](languages/go.md) Go | `**/*.go` | — |
| [`java`](languages/java.md) Java | `**/*.java` | spring-boot, kotlin |
| [`kotlin`](languages/kotlin.md) Kotlin | `**/*.kt`, `**/*.kts` | java, jetpack-compose, spring-boot |
| [`python`](languages/python.md) Python | `**/*.py`, `**/*.pyi` | fastapi, django, pytorch |
| [`rust`](languages/rust.md) Rust | `**/*.rs` | — |
| [`shell`](languages/shell.md) Shell (Bash, sh, PowerShell) | `**/*.sh`, `**/*.bash`, `**/*.ps1`, `**/*.psm1` | docker, github-actions |
| [`sql`](languages/sql.md) SQL | `**/*.sql` | dbt |
| [`swift`](languages/swift.md) Swift | `**/*.swift` | swiftui |
| [`typescript`](languages/typescript.md) TypeScript / JavaScript | `**/*.ts`, `**/*.tsx`, `**/*.mts`, `**/*.cts`, `**/*.js`, `**/*.jsx`, `**/*.mjs`, `**/*.cjs` | react, nextjs, vue, node-server, react-native |

## framework

| Pack | Default paths | Related |
|---|---|---|
| [`aspnet-core`](frameworks/aspnet-core.md) ASP.NET Core | — | csharp, sql, docker |
| [`dbt`](frameworks/dbt.md) dbt | `**/models/**/*.sql`, `**/models/**/*.yml`, `**/macros/**/*.sql`, `**/dbt_project.yml` | sql, python |
| [`django`](frameworks/django.md) Django | `**/models.py`, `**/views.py`, `**/serializers.py`, `**/urls.py`, `**/migrations/*.py`, `**/settings*.py`, `**/settings/*.py` | python, sql |
| [`fastapi`](frameworks/fastapi.md) FastAPI | — | python, sql, docker |
| [`flutter`](frameworks/flutter.md) Flutter | `**/lib/**/*.dart` | dart |
| [`godot`](frameworks/godot.md) Godot | `**/*.gd`, `**/*.tscn`, `**/*.tres`, `**/*.gdshader`, `**/project.godot` | csharp |
| [`jetpack-compose`](frameworks/jetpack-compose.md) Jetpack Compose | — | kotlin |
| [`nextjs`](frameworks/nextjs.md) Next.js | `**/app/**/*.tsx`, `**/app/**/route.ts`, `**/pages/**/*.tsx`, `**/next.config.*` | react, typescript, node-server |
| [`node-server`](frameworks/node-server.md) Node.js Server Frameworks | — | typescript, sql, docker |
| [`pytorch`](frameworks/pytorch.md) PyTorch | — | python |
| [`react`](frameworks/react.md) React | `**/*.jsx`, `**/*.tsx` | typescript, nextjs, react-native |
| [`react-native`](frameworks/react-native.md) React Native | — | react, typescript, kotlin, swift |
| [`spring-boot`](frameworks/spring-boot.md) Spring Boot | — | java, kotlin, sql, docker |
| [`swiftui`](frameworks/swiftui.md) SwiftUI | — | swift |
| [`unity`](frameworks/unity.md) Unity | `**/Assets/**/*.cs`, `**/Packages/**/*.cs`, `**/*.asmdef`, `**/*.asmref`, `**/*.unity`, `**/*.prefab`, `**/*.asset` | csharp |
| [`unreal`](frameworks/unreal.md) Unreal Engine | `**/Source/**/*.cpp`, `**/Source/**/*.h`, `**/*.Build.cs`, `**/*.Target.cs`, `**/*.uproject`, `**/*.uplugin` | cpp |
| [`vue`](frameworks/vue.md) Vue | `**/*.vue` | typescript |

## infra

| Pack | Default paths | Related |
|---|---|---|
| [`docker`](infra/docker.md) Docker | `**/Dockerfile*`, `**/*.dockerfile`, `**/.dockerignore`, `**/compose*.y*ml`, `**/docker-compose*.y*ml`, `**/docker-bake.hcl` | kubernetes, github-actions, shell |
| [`github-actions`](infra/github-actions.md) GitHub Actions | `.github/workflows/*.y*ml`, `.github/actions/**` | docker, terraform, shell |
| [`kubernetes`](infra/kubernetes.md) Kubernetes | `**/k8s/**/*.y*ml`, `**/charts/**`, `**/Chart.yaml`, `**/kustomization.y*ml` | docker, terraform, github-actions |
| [`terraform`](infra/terraform.md) Terraform / OpenTofu | `**/*.tf`, `**/*.tfvars`, `**/*.tofu`, `**/*.tftest.hcl`, `**/.terraform.lock.hcl` | kubernetes, docker, github-actions |
