using System.Globalization;

namespace Tamp.Go;

/// <summary>Settings for <c>go build</c> — compile packages and dependencies.</summary>
public sealed class GoBuildSettings : GoBuildLikeSettingsBase
{
    /// <summary>Output path (<c>-o</c>) — a file (single package) or a directory (multiple). Required for most packaging handoffs.</summary>
    public string? Output { get; set; }

    public GoBuildSettings SetOutput(string? path) { Output = path; return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "build";
        if (!string.IsNullOrEmpty(Output)) { yield return "-o"; yield return Output!; }
        foreach (var a in EmitBuildLikeFlags()) yield return a;
    }
}

/// <summary>Settings for <c>go test</c> — compile and run tests.</summary>
public sealed class GoTestSettings : GoBuildLikeSettingsBase
{
    /// <summary>Run only tests matching this regexp (<c>-run</c>).</summary>
    public string? RunFilter { get; set; }

    /// <summary>Run benchmarks matching this regexp (<c>-bench</c>) — e.g. <c>.</c> for all.</summary>
    public string? BenchFilter { get; set; }

    /// <summary>Report memory allocations for benchmarks (<c>-benchmem</c>).</summary>
    public bool BenchMem { get; set; }

    /// <summary>Run each test/benchmark n times (<c>-count</c>). <c>-count=1</c> is the idiomatic way to bypass the test cache.</summary>
    public int? Count { get; set; }

    /// <summary>Run in short mode (<c>-short</c>) — tests gate expensive cases behind <c>testing.Short()</c>.</summary>
    public bool Short { get; set; }

    /// <summary>Per-test timeout (<c>-timeout</c>) — e.g. <c>30s</c>, <c>5m</c>.</summary>
    public string? Timeout { get; set; }

    /// <summary>Write a coverage profile (<c>-coverprofile</c>) — e.g. <c>coverage.out</c>.</summary>
    public string? CoverProfile { get; set; }

    /// <summary>Emit test results as a JSON stream (<c>-json</c>) — the machine-readable form for result ingestion.</summary>
    public bool Json { get; set; }

    /// <summary>Stop on first test failure (<c>-failfast</c>).</summary>
    public bool FailFast { get; set; }

    /// <summary>Parallelism within a test binary (<c>-parallel</c>).</summary>
    public int? ParallelTests { get; set; }

    /// <summary>Shuffle test/benchmark order (<c>-shuffle</c>) — <c>on</c>, <c>off</c>, or a seed.</summary>
    public string? Shuffle { get; set; }

    /// <summary>Disable the implicit <c>go vet</c> pass that <c>go test</c> runs (<c>-vet=off</c>).</summary>
    public bool VetOff { get; set; }

    /// <summary>Compile the test binary but do not run it (<c>-c</c>). Pairs with <see cref="Output"/> to split compile + run CI stages.</summary>
    public bool CompileOnly { get; set; }

    /// <summary>Output path for the compiled test binary (<c>-o</c>), used with <see cref="CompileOnly"/>.</summary>
    public string? Output { get; set; }

    /// <summary>Flags forwarded to the test binary after <c>-args</c> (emitted last, after the package patterns).</summary>
    public List<string> ExtraArgs { get; } = new();

    public GoTestSettings SetRunFilter(string? r) { RunFilter = r; return this; }
    public GoTestSettings SetBenchFilter(string? r) { BenchFilter = r; return this; }
    public GoTestSettings SetBenchMem(bool v = true) { BenchMem = v; return this; }
    public GoTestSettings SetCount(int? n) { Count = n; return this; }
    public GoTestSettings SetShort(bool v = true) { Short = v; return this; }
    public GoTestSettings SetTimeout(string? d) { Timeout = d; return this; }
    public GoTestSettings SetCoverProfile(string? path) { CoverProfile = path; return this; }
    public GoTestSettings SetJson(bool v = true) { Json = v; return this; }
    public GoTestSettings SetFailFast(bool v = true) { FailFast = v; return this; }
    public GoTestSettings SetParallelTests(int? n) { ParallelTests = n; return this; }
    public GoTestSettings SetShuffle(string? v) { Shuffle = v; return this; }
    public GoTestSettings SetVetOff(bool v = true) { VetOff = v; return this; }
    public GoTestSettings SetCompileOnly(bool v = true) { CompileOnly = v; return this; }
    public GoTestSettings SetOutput(string? path) { Output = path; return this; }
    public GoTestSettings AddExtraArg(string arg) { ExtraArgs.Add(arg); return this; }
    public GoTestSettings AddExtraArgs(params string[] args) { ExtraArgs.AddRange(args); return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "test";
        if (CompileOnly) yield return "-c";
        if (!string.IsNullOrEmpty(Output)) { yield return "-o"; yield return Output!; }
        foreach (var a in EmitBuildLikeFlags()) yield return a;
        if (!string.IsNullOrEmpty(RunFilter)) { yield return "-run"; yield return RunFilter!; }
        if (!string.IsNullOrEmpty(BenchFilter)) { yield return "-bench"; yield return BenchFilter!; }
        if (BenchMem) yield return "-benchmem";
        if (Count is { } c) { yield return "-count"; yield return c.ToString(CultureInfo.InvariantCulture); }
        if (Short) yield return "-short";
        if (!string.IsNullOrEmpty(Timeout)) { yield return "-timeout"; yield return Timeout!; }
        if (!string.IsNullOrEmpty(CoverProfile)) { yield return "-coverprofile"; yield return CoverProfile!; }
        if (Json) yield return "-json";
        if (FailFast) yield return "-failfast";
        if (ParallelTests is { } pt) { yield return "-parallel"; yield return pt.ToString(CultureInfo.InvariantCulture); }
        if (!string.IsNullOrEmpty(Shuffle)) yield return $"-shuffle={Shuffle}";
        if (VetOff) yield return "-vet=off";
    }

    // Packages come first, then `-args <extra...>` to the test binary (must be last).
    protected override IEnumerable<string> BuildTrailingArguments()
    {
        foreach (var p in Packages) yield return p;
        if (ExtraArgs.Count > 0)
        {
            yield return "-args";
            foreach (var a in ExtraArgs) yield return a;
        }
    }
}

/// <summary>Settings for <c>go vet</c> — report likely mistakes. The built-in static-analysis CI gate.</summary>
public sealed class GoVetSettings : GoBuildLikeSettingsBase
{
    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "vet";
        // vet accepts the build flags that affect package loading (-tags, -mod, -v, -x).
        foreach (var a in EmitBuildLikeFlags()) yield return a;
    }
}

/// <summary>Settings for <c>go run</c> — compile and run a main package.</summary>
public sealed class GoRunSettings : GoBuildLikeSettingsBase
{
    /// <summary>Arguments forwarded to the program being run (emitted after the package pattern).</summary>
    public List<string> ProgramArgs { get; } = new();

    public GoRunSettings AddProgramArg(string arg) { ProgramArgs.Add(arg); return this; }
    public GoRunSettings AddProgramArgs(params string[] args) { ProgramArgs.AddRange(args); return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "run";
        foreach (var a in EmitBuildLikeFlags()) yield return a;
    }

    protected override IEnumerable<string> BuildTrailingArguments()
    {
        foreach (var p in Packages) yield return p;
        foreach (var a in ProgramArgs) yield return a;
    }
}

/// <summary>Settings for <c>go install</c> — compile and install packages to <c>GOBIN</c>.</summary>
public sealed class GoInstallSettings : GoBuildLikeSettingsBase
{
    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "install";
        foreach (var a in EmitBuildLikeFlags()) yield return a;
    }
}

/// <summary>Settings for <c>go list</c> — list packages or modules. Great for discovery; pair with <see cref="Json"/>.</summary>
public sealed class GoListSettings : GoBuildLikeSettingsBase
{
    /// <summary>Emit full JSON records (<c>-json</c>).</summary>
    public bool Json { get; set; }

    /// <summary>Go-template format string (<c>-f</c>) — e.g. <c>{{.ImportPath}}</c>.</summary>
    public string? Format { get; set; }

    /// <summary>List modules instead of packages (<c>-m</c>).</summary>
    public bool Modules { get; set; }

    /// <summary>Include all dependencies transitively (<c>-deps</c>).</summary>
    public bool Deps { get; set; }

    /// <summary>With <see cref="Modules"/>, list available upgrades (<c>-u</c>).</summary>
    public bool Updates { get; set; }

    public GoListSettings SetJson(bool v = true) { Json = v; return this; }
    public GoListSettings SetFormat(string? f) { Format = f; return this; }
    public GoListSettings SetModules(bool v = true) { Modules = v; return this; }
    public GoListSettings SetDeps(bool v = true) { Deps = v; return this; }
    public GoListSettings SetUpdates(bool v = true) { Updates = v; return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "list";
        if (Modules) yield return "-m";
        if (Deps) yield return "-deps";
        if (Updates) yield return "-u";
        if (Json) yield return "-json";
        if (!string.IsNullOrEmpty(Format)) { yield return "-f"; yield return Format!; }
        foreach (var a in EmitBuildLikeFlags()) yield return a;
    }
}

/// <summary>Settings for <c>go generate</c> — run code generators named by <c>//go:generate</c> directives.</summary>
public sealed class GoGenerateSettings : GoSettingsBase
{
    /// <summary>Only run directives whose command matches this regexp (<c>-run</c>).</summary>
    public string? RunFilter { get; set; }

    /// <summary>Build constraint tags (<c>-tags</c>, comma-joined).</summary>
    public List<string> Tags { get; } = new();

    /// <summary>Print commands without running them (<c>-n</c>).</summary>
    public bool DryRun { get; set; }

    /// <summary>Verbose (<c>-v</c>).</summary>
    public bool Verbose { get; set; }

    /// <summary>Package patterns (positional). Default is the current directory.</summary>
    public List<string> Packages { get; } = new();

    public GoGenerateSettings SetRunFilter(string? r) { RunFilter = r; return this; }
    public GoGenerateSettings AddTag(string tag) { Tags.Add(tag); return this; }
    public GoGenerateSettings SetDryRun(bool v = true) { DryRun = v; return this; }
    public GoGenerateSettings SetVerbose(bool v = true) { Verbose = v; return this; }
    public GoGenerateSettings AddPackage(string pattern) { Packages.Add(pattern); return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "generate";
        if (!string.IsNullOrEmpty(RunFilter)) { yield return "-run"; yield return RunFilter!; }
        if (Tags.Count > 0) { yield return "-tags"; yield return string.Join(",", Tags); }
        if (DryRun) yield return "-n";
        if (Verbose) yield return "-v";
    }

    protected override IEnumerable<string> BuildTrailingArguments() => Packages;
}

/// <summary>Settings for <c>go fmt</c> — run gofmt over packages.</summary>
public sealed class GoFmtSettings : GoSettingsBase
{
    /// <summary>Print commands without running them (<c>-n</c>).</summary>
    public bool DryRun { get; set; }

    /// <summary>Package patterns (positional). Default is the current directory.</summary>
    public List<string> Packages { get; } = new();

    public GoFmtSettings SetDryRun(bool v = true) { DryRun = v; return this; }
    public GoFmtSettings AddPackage(string pattern) { Packages.Add(pattern); return this; }
    public GoFmtSettings AllPackages() { Packages.Add("./..."); return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "fmt";
        if (DryRun) yield return "-n";
    }

    protected override IEnumerable<string> BuildTrailingArguments() => Packages;
}

/// <summary>Settings for <c>go clean</c> — remove object files and cached artifacts.</summary>
public sealed class GoCleanSettings : GoSettingsBase
{
    /// <summary>Clean the build cache (<c>-cache</c>).</summary>
    public bool Cache { get; set; }

    /// <summary>Clean the test result cache (<c>-testcache</c>).</summary>
    public bool TestCache { get; set; }

    /// <summary>Clean the module download cache (<c>-modcache</c>).</summary>
    public bool ModCache { get; set; }

    /// <summary>Clean the fuzz cache (<c>-fuzzcache</c>).</summary>
    public bool FuzzCache { get; set; }

    /// <summary>Package patterns (positional).</summary>
    public List<string> Packages { get; } = new();

    public GoCleanSettings SetCache(bool v = true) { Cache = v; return this; }
    public GoCleanSettings SetTestCache(bool v = true) { TestCache = v; return this; }
    public GoCleanSettings SetModCache(bool v = true) { ModCache = v; return this; }
    public GoCleanSettings SetFuzzCache(bool v = true) { FuzzCache = v; return this; }
    public GoCleanSettings AddPackage(string pattern) { Packages.Add(pattern); return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "clean";
        if (Cache) yield return "-cache";
        if (TestCache) yield return "-testcache";
        if (ModCache) yield return "-modcache";
        if (FuzzCache) yield return "-fuzzcache";
    }

    protected override IEnumerable<string> BuildTrailingArguments() => Packages;
}

/// <summary>Settings for <c>go env</c> — print (or set) Go environment values.</summary>
public sealed class GoEnvSettings : GoSettingsBase
{
    /// <summary>Emit as JSON (<c>-json</c>).</summary>
    public bool Json { get; set; }

    /// <summary>Specific variable names to print (positional) — empty prints all.</summary>
    public List<string> Names { get; } = new();

    public GoEnvSettings SetJson(bool v = true) { Json = v; return this; }
    public GoEnvSettings AddName(string name) { Names.Add(name); return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "env";
        if (Json) yield return "-json";
    }

    protected override IEnumerable<string> BuildTrailingArguments() => Names;
}

/// <summary>Settings for <c>go version</c> — print the toolchain version, or the versions embedded in binaries.</summary>
public sealed class GoVersionSettings : GoSettingsBase
{
    /// <summary>Report module versions embedded in the named binaries (<c>-m</c>).</summary>
    public bool Modules { get; set; }

    /// <summary>Binary/file paths to inspect (positional).</summary>
    public List<string> Files { get; } = new();

    public GoVersionSettings SetModules(bool v = true) { Modules = v; return this; }
    public GoVersionSettings AddFile(string path) { Files.Add(path); return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "version";
        if (Modules) yield return "-m";
    }

    protected override IEnumerable<string> BuildTrailingArguments() => Files;
}

/// <summary>Settings for <c>go mod tidy</c> — add missing and remove unused modules.</summary>
public sealed class GoModTidySettings : GoSettingsBase
{
    /// <summary>Pin the <c>go</c> directive the tidy targets (<c>-go</c>) — e.g. <c>1.23</c>.</summary>
    public string? Go { get; set; }

    /// <summary>Preserve additional module graph for the given version (<c>-compat</c>).</summary>
    public string? Compat { get; set; }

    /// <summary>Print the commands (<c>-x</c>).</summary>
    public bool PrintCommands { get; set; }

    public GoModTidySettings SetGo(string? v) { Go = v; return this; }
    public GoModTidySettings SetCompat(string? v) { Compat = v; return this; }
    public GoModTidySettings SetPrintCommands(bool v = true) { PrintCommands = v; return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "mod";
        yield return "tidy";
        if (!string.IsNullOrEmpty(Go)) yield return $"-go={Go}";
        if (!string.IsNullOrEmpty(Compat)) yield return $"-compat={Compat}";
        if (PrintCommands) yield return "-x";
    }
}

/// <summary>Settings for <c>go mod download</c> — download modules to the local cache.</summary>
public sealed class GoModDownloadSettings : GoSettingsBase
{
    /// <summary>Emit JSON records (<c>-json</c>).</summary>
    public bool Json { get; set; }

    /// <summary>Print the commands (<c>-x</c>).</summary>
    public bool PrintCommands { get; set; }

    /// <summary>Specific module paths to download (positional) — empty downloads all in the build list.</summary>
    public List<string> Modules { get; } = new();

    public GoModDownloadSettings SetJson(bool v = true) { Json = v; return this; }
    public GoModDownloadSettings SetPrintCommands(bool v = true) { PrintCommands = v; return this; }
    public GoModDownloadSettings AddModule(string path) { Modules.Add(path); return this; }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "mod";
        yield return "download";
        if (Json) yield return "-json";
        if (PrintCommands) yield return "-x";
    }

    protected override IEnumerable<string> BuildTrailingArguments() => Modules;
}

/// <summary>Settings for <c>go mod verify</c> — verify that cached dependencies match their checksums.</summary>
public sealed class GoModVerifySettings : GoSettingsBase
{
    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "mod";
        yield return "verify";
    }
}

/// <summary>Settings for <c>go work sync</c> — push the workspace build list back to member modules.</summary>
public sealed class GoWorkSyncSettings : GoSettingsBase
{
    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "work";
        yield return "sync";
    }
}

/// <summary>Raw escape hatch — emits <c>go &lt;args...&gt;</c>. Use only when the typed verbs don't cover what you need.</summary>
public sealed class GoRawSettings : GoSettingsBase
{
    private readonly List<string> _args = new();
    public void AddArgs(IEnumerable<string> args) => _args.AddRange(args);
    protected override IEnumerable<string> BuildVerbArguments() => _args;
}
