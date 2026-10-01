namespace Tamp.Go;

/// <summary>Top-level facade for <c>go</c> verbs.</summary>
/// <remarks>
/// <para>Resolve the tool via <c>[FromPath("go")]</c>:</para>
/// <code>
/// [FromPath("go")] readonly Tool GoBin = null!;
/// </code>
/// <para>
/// The <c>go</c> command is installed from go.dev or a distro package. The Tool resolves to
/// <c>go</c> on PATH. Cross-compilation is selected through <c>GOOS</c>/<c>GOARCH</c>/<c>CGO_ENABLED</c>
/// (see <see cref="GoSettingsBase"/>), not a target-triple flag.
/// </para>
/// </remarks>
public static class Go
{
    /// <summary><c>go build</c> — compile packages and dependencies. Set <c>SetOutput(...)</c> to control the artifact path.</summary>
    public static CommandPlan Build(Tool tool, Action<GoBuildSettings>? configure = null)
        => Make<GoBuildSettings>(tool, configure);

    /// <summary><c>go test</c> — compile and run tests. Use <c>SetCover()</c>/<c>SetCoverProfile(...)</c> for the coverage lane.</summary>
    public static CommandPlan Test(Tool tool, Action<GoTestSettings>? configure = null)
        => Make<GoTestSettings>(tool, configure);

    /// <summary><c>go vet</c> — the built-in static-analysis gate.</summary>
    public static CommandPlan Vet(Tool tool, Action<GoVetSettings>? configure = null)
        => Make<GoVetSettings>(tool, configure);

    /// <summary><c>go run</c> — compile and run a main package. Forward program args with <c>AddProgramArg(...)</c>.</summary>
    public static CommandPlan Run_(Tool tool, Action<GoRunSettings>? configure = null)
        => Make<GoRunSettings>(tool, configure);

    /// <summary><c>go install</c> — compile and install to GOBIN.</summary>
    public static CommandPlan Install(Tool tool, Action<GoInstallSettings>? configure = null)
        => Make<GoInstallSettings>(tool, configure);

    /// <summary><c>go list</c> — list packages or modules; pair with <c>SetJson()</c> for machine-readable discovery.</summary>
    public static CommandPlan List(Tool tool, Action<GoListSettings>? configure = null)
        => Make<GoListSettings>(tool, configure);

    /// <summary><c>go generate</c> — run <c>//go:generate</c> directives.</summary>
    public static CommandPlan Generate(Tool tool, Action<GoGenerateSettings>? configure = null)
        => Make<GoGenerateSettings>(tool, configure);

    /// <summary><c>go fmt</c> — run gofmt over packages.</summary>
    public static CommandPlan Fmt(Tool tool, Action<GoFmtSettings>? configure = null)
        => Make<GoFmtSettings>(tool, configure);

    /// <summary><c>go clean</c> — remove object files and (optionally) caches.</summary>
    public static CommandPlan Clean(Tool tool, Action<GoCleanSettings>? configure = null)
        => Make<GoCleanSettings>(tool, configure);

    /// <summary><c>go env</c> — print Go environment values.</summary>
    public static CommandPlan Env(Tool tool, Action<GoEnvSettings>? configure = null)
        => Make<GoEnvSettings>(tool, configure);

    /// <summary><c>go version</c> — print the toolchain version, or (<c>-m</c>) versions embedded in binaries.</summary>
    public static CommandPlan Version(Tool tool, Action<GoVersionSettings>? configure = null)
        => Make<GoVersionSettings>(tool, configure);

    /// <summary><c>go mod &lt;...&gt;</c> sub-facade.</summary>
    public static class Mod
    {
        /// <summary><c>go mod tidy</c> — add missing and remove unused modules.</summary>
        public static CommandPlan Tidy(Tool tool, Action<GoModTidySettings>? configure = null)
            => Make<GoModTidySettings>(tool, configure);

        /// <summary><c>go mod download</c> — populate the local module cache. The recommended CI restore step.</summary>
        public static CommandPlan Download(Tool tool, Action<GoModDownloadSettings>? configure = null)
            => Make<GoModDownloadSettings>(tool, configure);

        /// <summary><c>go mod verify</c> — verify cached dependencies against their checksums.</summary>
        public static CommandPlan Verify(Tool tool, Action<GoModVerifySettings>? configure = null)
            => Make<GoModVerifySettings>(tool, configure);
    }

    /// <summary><c>go work &lt;...&gt;</c> sub-facade.</summary>
    public static class Work
    {
        /// <summary><c>go work sync</c> — sync the workspace build list to member modules.</summary>
        public static CommandPlan Sync(Tool tool, Action<GoWorkSyncSettings>? configure = null)
            => Make<GoWorkSyncSettings>(tool, configure);
    }

    /// <summary>
    /// Read the <c>module</c> path from a <c>go.mod</c> manifest, or <c>null</c> if the file has no
    /// module directive.
    /// </summary>
    public static string? GetModulePath(AbsolutePath goModPath)
    {
        if (goModPath is null) throw new ArgumentNullException(nameof(goModPath));
        if (!File.Exists(goModPath.Value)) return null;
        return GoMod.GetModulePath(File.ReadAllText(goModPath.Value));
    }

    /// <summary>
    /// Read the <c>go</c> language-version directive (e.g. <c>1.23</c>) from a <c>go.mod</c> manifest,
    /// or <c>null</c> if absent.
    /// </summary>
    /// <remarks>
    /// Go modules carry no package <i>version</i> field — a module's version is its git tag, not a line
    /// in <c>go.mod</c>. Build-time version stamping is therefore done with linker flags
    /// (<see cref="GoBuildLikeSettingsBaseExtensions.SetVersionVariable{T}"/>), not by editing the manifest.
    /// </remarks>
    public static string? GetGoDirective(AbsolutePath goModPath)
    {
        if (goModPath is null) throw new ArgumentNullException(nameof(goModPath));
        if (!File.Exists(goModPath.Value)) return null;
        return GoMod.GetGoDirective(File.ReadAllText(goModPath.Value));
    }

    /// <summary>Raw escape hatch — for verbs the typed surface doesn't cover (e.g. <c>go tool</c>, <c>go doc</c>, <c>go work init</c>).</summary>
    public static CommandPlan Raw(Tool tool, params string[] arguments)
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        if (arguments is null || arguments.Length == 0)
            throw new ArgumentException("Raw requires at least one argument.", nameof(arguments));
        var s = new GoRawSettings();
        s.AddArgs(arguments);
        return s.ToCommandPlan(tool);
    }

    private static CommandPlan Make<T>(Tool tool, Action<T>? configure) where T : GoSettingsBase, new()
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        var s = new T();
        configure?.Invoke(s);
        return s.ToCommandPlan(tool);
    }

    // ---- Object-init overloads ----

    public static CommandPlan Build(Tool tool, GoBuildSettings settings) => Plan(tool, settings);
    public static CommandPlan Test(Tool tool, GoTestSettings settings) => Plan(tool, settings);
    public static CommandPlan Vet(Tool tool, GoVetSettings settings) => Plan(tool, settings);
    public static CommandPlan Run_(Tool tool, GoRunSettings settings) => Plan(tool, settings);
    public static CommandPlan Install(Tool tool, GoInstallSettings settings) => Plan(tool, settings);
    public static CommandPlan List(Tool tool, GoListSettings settings) => Plan(tool, settings);
    public static CommandPlan Generate(Tool tool, GoGenerateSettings settings) => Plan(tool, settings);
    public static CommandPlan Fmt(Tool tool, GoFmtSettings settings) => Plan(tool, settings);
    public static CommandPlan Clean(Tool tool, GoCleanSettings settings) => Plan(tool, settings);

    private static CommandPlan Plan<T>(Tool tool, T settings) where T : GoSettingsBase
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        return settings.ToCommandPlan(tool);
    }
}
