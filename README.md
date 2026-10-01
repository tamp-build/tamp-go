# Tamp.Go

> Wrapper for the `go` CLI — the Go toolchain build driver. A Tamp satellite; same shape and conventions as [`Tamp.Cargo`](https://github.com/tamp-build/tamp-cargo).

| Package | Status |
|---|---|
| `Tamp.Go` | 0.1.0 (initial) |

## Why Tamp drives Go

A Go build that ships real binaries is rarely just `go build`. It's a cross-compile matrix (`GOOS`/`GOARCH`), a version stamped into the binary with `-ldflags -X`, a `-trimpath` for reproducibility, a `go vet` gate, a `-race` test lane, a coverage profile, and a module restore that must be `-mod=readonly` in CI. Today that logic lives half in a Makefile and half in YAML.

`Tamp.Go` puts those verbs into a typed, composable, dependency-graph-aware C# build script. The script *is* the contract: which package compiles, for which platform, where the output lands, which step depends on which.

## Install

```bash
dotnet add package Tamp.Go
```

Multi-targets net8 / net9 / net10. Requires `go` on PATH ([go.dev/dl](https://go.dev/dl/)).

## Quick start

```csharp
using Tamp;
using Tamp.Go;

class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [FromPath("go")] readonly Tool GoBin = null!;

    AbsolutePath Module => RootDirectory / "src";
    AbsolutePath Out => RootDirectory / "artifacts";

    [Parameter] readonly string Version = "0.0.0-dev";

    // [CI gate] restore (readonly), vet, test with race + coverage
    Target Restore => _ => _
        .Executes(() => Go.Mod.Download(GoBin, s => s.SetWorkingDirectory(Module)));

    Target Vet => _ => _
        .DependsOn(nameof(Restore))
        .Executes(() => Go.Vet(GoBin, s => s
            .SetWorkingDirectory(Module)
            .SetReadonly()
            .AllPackages()));

    Target Test => _ => _
        .DependsOn(nameof(Vet))
        .Executes(() => Go.Test(GoBin, s => s
            .SetWorkingDirectory(Module)
            .SetRace()
            .SetCover()
            .SetCoverProfile("coverage.out")
            .SetCoverMode("atomic")   // required with -race
            .AllPackages()));

    // [Build] static linux/arm64 release binary, version stamped
    Target BuildLinuxArm64 => _ => _
        .DependsOn(nameof(Test))
        .Executes(() => Go.Build(GoBin, s => s
            .SetWorkingDirectory(Module)
            .SetPlatform("linux", "arm64")
            .SetCgoEnabled(false)
            .SetTrimpath()
            .SetVersionVariable("main.version", Version)
            .StripDebugInfo()
            .SetOutput(Out / "server-linux-arm64")
            .AddPackage("./cmd/server")));
}
```

Run it: `dotnet tamp Test`, `dotnet tamp BuildLinuxArm64`, or `dotnet tamp BuildLinuxArm64 --dry-run` to print the `CommandPlan`s without executing.

## Verb surface

| Facade | Command | Notes |
|---|---|---|
| `Go.Build` | `go build` | `-o`, build tags, ldflags, `-trimpath`, `-race`, `-cover`, `-buildmode` |
| `Go.Test` | `go test` | `-run`, `-bench`, `-cover`/`-coverprofile`, `-json`, `-count`, `-timeout`, `-c`, `-args` |
| `Go.Vet` | `go vet` | built-in static-analysis gate |
| `Go.Run_` | `go run` | forwards program args after the package |
| `Go.Install` | `go install` | install to `GOBIN` |
| `Go.List` | `go list` | `-json`, `-m`, `-deps`, `-f` — discovery |
| `Go.Generate` | `go generate` | `//go:generate` directives |
| `Go.Fmt` | `go fmt` | gofmt over packages |
| `Go.Clean` | `go clean` | `-cache`, `-testcache`, `-modcache`, `-fuzzcache` |
| `Go.Env` / `Go.Version` | `go env` / `go version` | inspection |
| `Go.Mod.Tidy/Download/Verify` | `go mod …` | module management |
| `Go.Work.Sync` | `go work sync` | multi-module workspaces |
| `Go.Raw` | `go <args…>` | escape hatch (e.g. `go tool cover`) |

Plus two read-only helpers: `Go.GetModulePath(goMod)` and `Go.GetGoDirective(goMod)`.

## The two Go-specific design points

1. **Cross-compilation is environment-driven.** Unlike cargo's `--target <triple>`, Go selects the platform via `GOOS`/`GOARCH`/`GOARM`/`CGO_ENABLED`. `SetPlatform(...)` / `SetCgoEnabled(...)` emit these into the `CommandPlan.Environment`, not the argument list.
2. **There is no manifest version to stamp.** A Go module's version is its git tag, not a field in `go.mod`. So instead of a `go.mod` version editor, `Tamp.Go` ships `SetVersionVariable("main.version", v)` → `-ldflags "-X main.version=v"`, the idiomatic Go build-time stamp.

## Relationship to the security satellites

`Tamp.Go` wraps the Go toolchain's *own* commands. It is **not** a SARIF producer — `go vet` has no SARIF output. For scanning Go code into the Tamp security pipeline, pair it with:

- **`tamp-golangci-lint`** (lint, SARIF) — *roadmap*
- **`tamp-osv-scanner`** / **`tamp-grype`** / **`tamp-trivy`** — SCA on the module graph (today)
- **`tamp-syft`** — SBOM for the built binary / module set (today)

## License

MIT © Scott Singleton. Part of the [Tamp](https://github.com/tamp-build) build-automation ecosystem.
