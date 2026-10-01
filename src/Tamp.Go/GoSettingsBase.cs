using System.Globalization;

namespace Tamp.Go;

/// <summary>
/// Common knobs shared by every <c>go</c> verb's settings class. Mirrors the shape used by
/// <c>Tamp.Cargo</c> — working directory + environment overlay + the toolchain-scope knobs
/// the Go toolchain reads from the <b>environment</b> rather than from flags.
/// </summary>
/// <remarks>
/// <para>
/// <b>Working directory matters.</b> The <c>go</c> command resolves the module it operates on
/// from the current directory upward (via <c>go.mod</c>). Set <see cref="WorkingDirectory"/> to
/// your module root explicitly — the runner's own cwd is the wrong default for multi-module
/// (go.work) repos.
/// </para>
/// <para>
/// <b>Cross-compilation is environment-driven, not flag-driven.</b> Unlike cargo's
/// <c>--target &lt;triple&gt;</c>, Go selects the target platform through the <c>GOOS</c>,
/// <c>GOARCH</c>, <c>GOARM</c> and <c>CGO_ENABLED</c> environment variables. Set them via
/// <see cref="Goos"/> / <see cref="Goarch"/> etc.; they are emitted into the
/// <see cref="CommandPlan.Environment"/> of the produced plan, layered on top of
/// <see cref="EnvironmentVariables"/>.
/// </para>
/// </remarks>
public abstract class GoSettingsBase
{
    /// <summary>Working directory for the spawned <c>go</c> process. Typically the module root.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Per-invocation environment variables on top of the inherited environment.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    /// <summary>Target OS (<c>GOOS</c>) — e.g. <c>linux</c>, <c>windows</c>, <c>darwin</c>. Optional; defaults to host.</summary>
    public string? Goos { get; set; }

    /// <summary>Target architecture (<c>GOARCH</c>) — e.g. <c>amd64</c>, <c>arm64</c>, <c>386</c>. Optional; defaults to host.</summary>
    public string? Goarch { get; set; }

    /// <summary>ARM variant (<c>GOARM</c>) — e.g. <c>5</c>, <c>6</c>, <c>7</c>. Only meaningful when <see cref="Goarch"/> is <c>arm</c>.</summary>
    public string? Goarm { get; set; }

    /// <summary>Cgo toggle (<c>CGO_ENABLED</c>). <c>false</c> → <c>0</c> (fully static, the CI default for cross-builds); <c>true</c> → <c>1</c>. Null leaves it inherited.</summary>
    public bool? CgoEnabled { get; set; }

    /// <summary>Subclasses produce the verb + flag argument list (NOT the trailing positional package patterns).</summary>
    protected abstract IEnumerable<string> BuildVerbArguments();

    /// <summary>Subclasses emit trailing positional arguments (package patterns like <c>./...</c>) that must come last. Default empty.</summary>
    protected virtual IEnumerable<string> BuildTrailingArguments() => Array.Empty<string>();

    /// <summary>Subclasses extend the secret list (e.g. a token for private-module fetches). Default empty.</summary>
    protected virtual IEnumerable<Secret> CollectSecrets() => Array.Empty<Secret>();

    internal CommandPlan ToCommandPlan(Tool tool)
    {
        var args = new List<string>();
        args.AddRange(BuildVerbArguments());
        args.AddRange(BuildTrailingArguments());

        var env = new Dictionary<string, string>(EnvironmentVariables);
        if (!string.IsNullOrEmpty(Goos)) env["GOOS"] = Goos!;
        if (!string.IsNullOrEmpty(Goarch)) env["GOARCH"] = Goarch!;
        if (!string.IsNullOrEmpty(Goarm)) env["GOARM"] = Goarm!;
        if (CgoEnabled is { } cgo) env["CGO_ENABLED"] = cgo ? "1" : "0";

        return new CommandPlan
        {
            Executable = tool.Executable.Value,
            Arguments = args,
            Environment = env,
            WorkingDirectory = WorkingDirectory ?? tool.WorkingDirectory,
            Secrets = CollectSecrets().ToList(),
        };
    }
}

/// <summary>Fluent setters for the common knobs.</summary>
public static class GoSettingsBaseExtensions
{
    public static T SetWorkingDirectory<T>(this T s, string? cwd) where T : GoSettingsBase { s.WorkingDirectory = cwd; return s; }
    public static T SetEnvironmentVariable<T>(this T s, string name, string value) where T : GoSettingsBase { s.EnvironmentVariables[name] = value; return s; }
    public static T SetGoos<T>(this T s, string? goos) where T : GoSettingsBase { s.Goos = goos; return s; }
    public static T SetGoarch<T>(this T s, string? goarch) where T : GoSettingsBase { s.Goarch = goarch; return s; }
    public static T SetGoarm<T>(this T s, string? goarm) where T : GoSettingsBase { s.Goarm = goarm; return s; }
    public static T SetCgoEnabled<T>(this T s, bool v) where T : GoSettingsBase { s.CgoEnabled = v; return s; }

    /// <summary>Convenience for the common cross-compile case — sets <c>GOOS</c> + <c>GOARCH</c> in one call.</summary>
    public static T SetPlatform<T>(this T s, string goos, string goarch) where T : GoSettingsBase
    {
        s.Goos = goos;
        s.Goarch = goarch;
        return s;
    }
}

/// <summary>
/// Convenience base for verbs that share the Go build-flag set: build, test, vet, run, install, list.
/// Covers build tags, linker/compiler flags (ldflags etc.), the race/cover instrumentation toggles,
/// <c>-mod</c> mode, parallelism, and trailing package patterns.
/// </summary>
public abstract class GoBuildLikeSettingsBase : GoSettingsBase
{
    /// <summary>Build constraint tags (<c>-tags</c>, comma-joined) — e.g. <c>integration</c>, <c>netgo</c>.</summary>
    public List<string> Tags { get; } = new();

    /// <summary>Linker flags passed to <c>-ldflags</c> (space-joined into one argument). Use <see cref="GoBuildLikeSettingsBaseExtensions.SetVersionVariable{T}"/> for <c>-X</c> version stamping.</summary>
    public List<string> Ldflags { get; } = new();

    /// <summary>Compiler flags passed to <c>-gcflags</c> (space-joined). E.g. <c>all=-N -l</c> for a debug build.</summary>
    public List<string> Gcflags { get; } = new();

    /// <summary>Assembler flags passed to <c>-asmflags</c> (space-joined).</summary>
    public List<string> Asmflags { get; } = new();

    /// <summary>Remove absolute file-system paths from the compiled binary (<c>-trimpath</c>). Recommended for reproducible/release builds.</summary>
    public bool Trimpath { get; set; }

    /// <summary>Enable the data race detector (<c>-race</c>). Requires cgo; implies a slower binary.</summary>
    public bool Race { get; set; }

    /// <summary>Enable the memory sanitizer (<c>-msan</c>). Linux/clang only.</summary>
    public bool Msan { get; set; }

    /// <summary>Enable the address sanitizer (<c>-asan</c>).</summary>
    public bool Asan { get; set; }

    /// <summary>Instrument for coverage (<c>-cover</c>; Go 1.20+ supports it at build scope, not just test).</summary>
    public bool Cover { get; set; }

    /// <summary>Coverage mode (<c>-covermode</c>) — <c>set</c>, <c>count</c>, or <c>atomic</c> (required with <c>-race</c>).</summary>
    public string? CoverMode { get; set; }

    /// <summary>Packages to include in coverage (<c>-coverpkg</c>, comma-joined).</summary>
    public List<string> CoverPkg { get; } = new();

    /// <summary>Module download mode (<c>-mod</c>) — <c>readonly</c> (CI default), <c>mod</c>, or <c>vendor</c>.</summary>
    public string? ModMode { get; set; }

    /// <summary>Build mode (<c>-buildmode</c>) — e.g. <c>default</c>, <c>c-shared</c>, <c>c-archive</c>, <c>pie</c>, <c>plugin</c>.</summary>
    public string? BuildMode { get; set; }

    /// <summary>Number of parallel build/test jobs (<c>-p N</c>). Default is <c>GOMAXPROCS</c>.</summary>
    public int? Parallelism { get; set; }

    /// <summary>Verbose output (<c>-v</c>) — print package names as they are compiled/tested.</summary>
    public bool Verbose { get; set; }

    /// <summary>Print the commands the toolchain runs (<c>-x</c>). Pairs with <see cref="Verbose"/> for CI diagnostics.</summary>
    public bool PrintCommands { get; set; }

    /// <summary>Trailing package patterns (positional) — e.g. <c>./...</c>, <c>./cmd/server</c>. Default is the current directory.</summary>
    public List<string> Packages { get; } = new();

    /// <summary>Emit the shared build flags. Package patterns are emitted separately as trailing positionals.</summary>
    protected IEnumerable<string> EmitBuildLikeFlags()
    {
        if (Tags.Count > 0) { yield return "-tags"; yield return string.Join(",", Tags); }
        if (Ldflags.Count > 0) { yield return "-ldflags"; yield return string.Join(" ", Ldflags); }
        if (Gcflags.Count > 0) { yield return "-gcflags"; yield return string.Join(" ", Gcflags); }
        if (Asmflags.Count > 0) { yield return "-asmflags"; yield return string.Join(" ", Asmflags); }
        if (Trimpath) yield return "-trimpath";
        if (Race) yield return "-race";
        if (Msan) yield return "-msan";
        if (Asan) yield return "-asan";
        if (Cover) yield return "-cover";
        if (!string.IsNullOrEmpty(CoverMode)) { yield return "-covermode"; yield return CoverMode!; }
        if (CoverPkg.Count > 0) { yield return "-coverpkg"; yield return string.Join(",", CoverPkg); }
        if (!string.IsNullOrEmpty(ModMode)) yield return $"-mod={ModMode}";
        if (!string.IsNullOrEmpty(BuildMode)) yield return $"-buildmode={BuildMode}";
        if (Parallelism is { } p) { yield return "-p"; yield return p.ToString(CultureInfo.InvariantCulture); }
        if (Verbose) yield return "-v";
        if (PrintCommands) yield return "-x";
    }

    /// <inheritdoc />
    protected override IEnumerable<string> BuildTrailingArguments() => Packages;
}

/// <summary>Fluent setters for the build-like knobs.</summary>
public static class GoBuildLikeSettingsBaseExtensions
{
    public static T AddTag<T>(this T s, string tag) where T : GoBuildLikeSettingsBase { s.Tags.Add(tag); return s; }
    public static T AddTags<T>(this T s, params string[] tags) where T : GoBuildLikeSettingsBase { s.Tags.AddRange(tags); return s; }
    public static T AddLdflag<T>(this T s, string flag) where T : GoBuildLikeSettingsBase { s.Ldflags.Add(flag); return s; }
    public static T AddGcflag<T>(this T s, string flag) where T : GoBuildLikeSettingsBase { s.Gcflags.Add(flag); return s; }
    public static T AddAsmflag<T>(this T s, string flag) where T : GoBuildLikeSettingsBase { s.Asmflags.Add(flag); return s; }
    public static T SetTrimpath<T>(this T s, bool v = true) where T : GoBuildLikeSettingsBase { s.Trimpath = v; return s; }
    public static T SetRace<T>(this T s, bool v = true) where T : GoBuildLikeSettingsBase { s.Race = v; return s; }
    public static T SetMsan<T>(this T s, bool v = true) where T : GoBuildLikeSettingsBase { s.Msan = v; return s; }
    public static T SetAsan<T>(this T s, bool v = true) where T : GoBuildLikeSettingsBase { s.Asan = v; return s; }
    public static T SetCover<T>(this T s, bool v = true) where T : GoBuildLikeSettingsBase { s.Cover = v; return s; }
    public static T SetCoverMode<T>(this T s, string? mode) where T : GoBuildLikeSettingsBase { s.CoverMode = mode; return s; }
    public static T AddCoverPkg<T>(this T s, string pkg) where T : GoBuildLikeSettingsBase { s.CoverPkg.Add(pkg); return s; }
    public static T SetModMode<T>(this T s, string? mode) where T : GoBuildLikeSettingsBase { s.ModMode = mode; return s; }
    public static T SetReadonly<T>(this T s) where T : GoBuildLikeSettingsBase { s.ModMode = "readonly"; return s; }
    public static T SetVendor<T>(this T s) where T : GoBuildLikeSettingsBase { s.ModMode = "vendor"; return s; }
    public static T SetBuildMode<T>(this T s, string? mode) where T : GoBuildLikeSettingsBase { s.BuildMode = mode; return s; }
    public static T SetParallelism<T>(this T s, int? n) where T : GoBuildLikeSettingsBase { s.Parallelism = n; return s; }
    public static T SetVerbose<T>(this T s, bool v = true) where T : GoBuildLikeSettingsBase { s.Verbose = v; return s; }
    public static T SetPrintCommands<T>(this T s, bool v = true) where T : GoBuildLikeSettingsBase { s.PrintCommands = v; return s; }
    public static T AddPackage<T>(this T s, string pattern) where T : GoBuildLikeSettingsBase { s.Packages.Add(pattern); return s; }
    public static T AddPackages<T>(this T s, params string[] patterns) where T : GoBuildLikeSettingsBase { s.Packages.AddRange(patterns); return s; }

    /// <summary>Add all packages recursively from the module root (<c>./...</c>) — the canonical CI pattern.</summary>
    public static T AllPackages<T>(this T s) where T : GoBuildLikeSettingsBase { s.Packages.Add("./..."); return s; }

    /// <summary>
    /// Add a linker <c>-X importpath.name=value</c> variable, the idiomatic Go build-time
    /// version-stamping mechanism. Example: <c>SetVersionVariable("main.version", "1.4.0")</c>
    /// emits <c>-ldflags "-X main.version=1.4.0"</c>.
    /// </summary>
    public static T SetVersionVariable<T>(this T s, string importPathDotName, string value) where T : GoBuildLikeSettingsBase
    {
        if (string.IsNullOrWhiteSpace(importPathDotName))
            throw new ArgumentException("Import-path-qualified name is required (e.g. 'main.version').", nameof(importPathDotName));
        s.Ldflags.Add($"-X {importPathDotName}={value}");
        return s;
    }

    /// <summary>Strip the symbol table and DWARF debug info (<c>-ldflags "-s -w"</c>) for a smaller release binary.</summary>
    public static T StripDebugInfo<T>(this T s) where T : GoBuildLikeSettingsBase
    {
        s.Ldflags.Add("-s");
        s.Ldflags.Add("-w");
        return s;
    }
}
