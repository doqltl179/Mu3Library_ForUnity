---
id: go
title: Go
kind: language
applies_to: ["**/*.go"]
related: []
volatility: volatile
reviewed: 2026-10-09
sources: ["https://go.dev/doc/", "https://go.dev/doc/effective_go", "https://go.dev/ref/mod", "https://go.dev/doc/toolchain", "https://go.dev/doc/devel/release", "https://go.dev/wiki/Go-Release-Cycle", "https://go.dev/doc/go1.26", "https://go.dev/doc/go1.27"]
---

# Go

## Detect
- `go.mod`: module path, `go` directive (minimum language version), `toolchain` and `tool` directives. `go.work` means a multi-module workspace; run commands from the module being changed.
- `vendor/` present: builds use vendored dependencies; run `go mod vendor` after any dependency change.
- Lint config: `.golangci.yml`/`.golangci.yaml` (golangci-lint) or `staticcheck.conf`.
- Build constraints (`//go:build` lines, `_linux.go`-style suffixes) decide which files compile per platform.
- Generated code: `//go:generate` directives and files headed `// Code generated ... DO NOT EDIT.`

## Conventions
- Format with `gofmt` (or `goimports`); never hand-align.
- Check every returned error. Wrap with context, `fmt.Errorf("load config: %w", err)`; test with `errors.Is`/`errors.As`, never by string match.
- Pass `context.Context` as the first parameter, named `ctx`, through every call that does I/O or may block; never store it in a struct; honor cancellation.
- Give every goroutine an owner and an exit path (context cancellation, channel close, or `errgroup`); the starter waits for it to finish.
- Only the sender closes a channel.
- Define small interfaces where they are consumed; return concrete types.
- `panic` only for programmer errors; return errors for everything else.
- Write table-driven tests with `t.Run(name, ...)`; mark helpers with `t.Helper()`; release resources with `t.Cleanup`.
- Package names are short, lowercase, without underscores; avoid catch-all names like `util`.
- Add dependencies with `go get <module>@<version>`, then `go mod tidy`.

## Verify
- Format: `commands.format`, or `gofmt -l .` printing nothing.
- Static checks: `go vet ./...`; `commands.lint`, or `staticcheck ./...` / `golangci-lint run` when configured.
- Tests: `commands.test`, or `go test -race ./...`; narrow with `go test -race -run '^TestName$' ./pkg/...`.
- Build: `go build ./...`; `go mod tidy` must leave `go.mod` and `go.sum` unchanged.

## Pitfalls
- A nil pointer stored in an interface makes the interface non-nil; return a literal `nil` for "no error".
- Writing to a nil map panics; concurrent map writes crash the process; guard maps with a mutex.
- Copying a value that contains a `sync.Mutex` copies the lock; pass pointers (`go vet` copylocks reports it).
- `append` may share the backing array with the source slice; copy before mutating a slice someone else holds.
- `defer` in a loop runs only when the function returns; move the loop body into a function.
- Unclosed `resp.Body`, `sql.Rows`, or files leak; `defer x.Close()` right after the error check.
- A goroutine blocked on a channel nobody reads leaks forever; `select` on `ctx.Done()` alongside the send.
- Map iteration order is random; sort keys when order matters.

## Version Notes
- Go 1.27 (2026-08-19) is the latest major release, so 1.27 and 1.26 are supported: each major release is supported until two newer major releases exist, and a major release ships every six months (as of 2026-10, per go.dev/doc/devel/release and the Go release cycle wiki).
- Check the `go` directive before using: `min`/`max`/`clear` built-ins and `slices`/`maps` packages (1.21); per-iteration loop variables (1.22); range-over-func iterators (1.23); `tool` directives (1.24); `sync.WaitGroup.Go` and `testing/synctest` (1.25); `new(expr)` and `errors.AsType` (1.26); generic methods, `encoding/json/v2`, and the `uuid` package (1.27) (as of 2026-10, per go.dev release notes).
- From 1.27, `go test` runs the `stdversion` vet check, which reports standard library symbols newer than the file's `go` version (as of 2026-10, per go.dev/doc/go1.27).
- From 1.26, `go mod init` writes `go 1.(N-1).0` for toolchain 1.N; run `go get go@<version>` to raise it before using newer features. `go fix ./...` applies modernizers that rewrite code to newer idioms; run it only as a separate change (as of 2026-10, per go.dev/doc/go1.26).
- A `go` or `toolchain` line newer than the local toolchain makes the default `GOTOOLCHAIN=auto` find or download that toolchain; with `GOTOOLCHAIN=local` the go command refuses to run instead (1.21+) (as of 2026-10, per go.dev/doc/toolchain).
