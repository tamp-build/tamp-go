# Changelog

All notable changes to **Tamp.Go** are recorded here. Format follows
[Keep a Changelog](https://keepachangelog.com/); versioning is [SemVer](https://semver.org/)
and tracks the *wrapper's* own evolution, not the version of the wrapped `go` toolchain.

## [Unreleased]

## [0.2.0] - 2026-11-20 - .NET 11 in, .NET 8 and .NET 9 out

### Changed

- **Multi-targets `net10.0;net11.0`** instead of `net8.0;net9.0;net10.0` ([ADR 0015](https://github.com/tamp-build/tamp/blob/main/docs/adr/0015-target-framework-strategy.md)). .NET 11 shipped 2026-11-10 and .NET 8 and .NET 9 reached end of support the same day; the rule is to add a TFM the day a release ships and drop one the day Microsoft ends its support. `global.json` pins SDK 11.0.100.

  **If you target `net8.0` or `net9.0`, stay on the previous version** — this one carries no assets your project can resolve.

- **`Tamp.*` pinned to 1.21.0**, which carries a SARIF fix worth knowing about if this project's output is read as evidence: before 1.21.0, a SARIF result whose scanner omitted `level` was read as `none` — the least severe value — instead of the declared `warning`, on every runtime before net11 ([tamp#166](https://github.com/tamp-build/tamp/issues/166)).

- **Publishing moved to nuget.org trusted publishing (OIDC).** The `tamp_nuget_api_key` org secret expired 2026-11-01 along with every other pre-2026-08-17 nuget.org key. The publish job now mints a short-lived key from its own OIDC token, so there is no long-lived credential to rotate or leak.

### Added — 0.1.0 (initial)

- **Typed verb surface** over the `go` CLI: `Build`, `Test`, `Vet`, `Run_`, `Install`,
  `List`, `Generate`, `Fmt`, `Clean`, `Env`, `Version`, the `Mod.{Tidy,Download,Verify}`
  and `Work.Sync` sub-facades, and a `Raw` escape hatch — all returning `CommandPlan`
  (nothing executes at call time; `--dry-run` prints the plan).
- **Environment-driven cross-compilation.** `SetGoos`/`SetGoarch`/`SetGoarm`/`SetCgoEnabled`
  (and the `SetPlatform(goos, goarch)` convenience) emit `GOOS`/`GOARCH`/`GOARM`/`CGO_ENABLED`
  into `CommandPlan.Environment` rather than the argument list — Go's platform-selection model,
  unlike cargo's `--target` flag.
- **Version stamping helpers.** `SetVersionVariable("main.version", v)` builds the idiomatic
  `-ldflags "-X main.version=v"`; `StripDebugInfo()` adds `-s -w`. Go modules carry no package
  version field, so there is no `go.mod` version editor (contrast `Tamp.Cargo`'s Cargo.toml editor).
- **Build-flag coverage** shared across build-like verbs: `-tags`, `-ldflags`/`-gcflags`/`-asmflags`,
  `-trimpath`, `-race`/`-msan`/`-asan`, `-cover`/`-covermode`/`-coverpkg`, `-mod=<mode>`,
  `-buildmode=<mode>`, `-p`, `-v`, `-x`, and trailing package patterns (`AllPackages()` → `./...`).
- **`go test` specifics:** `-run`, `-bench`/`-benchmem`, `-count`, `-short`, `-timeout`,
  `-coverprofile`, `-json`, `-failfast`, `-parallel`, `-shuffle`, `-vet=off`, `-c`/`-o`, and
  test-binary args after `-args` (emitted after the package patterns).
- **Read-only `go.mod` helpers:** `Go.GetModulePath(...)` and `Go.GetGoDirective(...)`.
- Multi-targets net8.0 / net9.0 / net10.0. Dogfoods its own release via `dotnet tamp Ci && Push`.

### Notes

- **Not a SARIF producer** — `go vet` has no SARIF output. Code scanning for the Tamp security
  pipeline is delegated to `tamp-osv-scanner` / `tamp-grype` / `tamp-syft` today, and a future
  `tamp-golangci-lint` for lint SARIF.
- No `V<major>` suffix in the package id: the `go` command honors the Go 1 compatibility promise,
  so there is no CLI-major to pin against (same rationale as `Tamp.Cargo`, `Tamp.Trivy`).
