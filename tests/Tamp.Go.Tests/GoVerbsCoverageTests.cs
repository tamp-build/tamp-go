using System.Collections.Generic;
using System.IO;
using Tamp;
using Tamp.Go;
using Xunit;

namespace Tamp.Go.Tests;

/// <summary>
/// Breadth coverage for the verbs, object-init overloads, go.mod helpers, and fluent setters not
/// already exercised by <see cref="GoTests"/> — every typed knob emits the flag it claims to.
/// </summary>
public sealed class GoVerbsCoverageTests
{
    private static Tool FakeTool() => new(AbsolutePath.Create("/fake/go"));

    private static int IndexOf(IReadOnlyList<string> args, string token)
    {
        for (var i = 0; i < args.Count; i++) if (args[i] == token) return i;
        return -1;
    }

    // ---------- verbs ----------

    [Fact]
    public void Install_Emits_Verb_And_Flags()
    {
        var plan = Go.Install(FakeTool(), s => s.SetReadonly().AllPackages());
        Assert.Equal("install", plan.Arguments[0]);
        Assert.Contains("-mod=readonly", plan.Arguments);
        Assert.Equal("./...", plan.Arguments[^1]);
    }

    [Fact]
    public void List_Modules_Deps_Updates_Json_Format()
    {
        var plan = Go.List(FakeTool(), s => s
            .SetModules().SetDeps().SetUpdates().SetJson()
            .SetFormat("{{.ImportPath}}").AddPackage("./..."));
        Assert.Equal("list", plan.Arguments[0]);
        Assert.Contains("-m", plan.Arguments);
        Assert.Contains("-deps", plan.Arguments);
        Assert.Contains("-u", plan.Arguments);
        Assert.Contains("-json", plan.Arguments);
        Assert.Equal("{{.ImportPath}}", plan.Arguments[IndexOf(plan.Arguments, "-f") + 1]);
        Assert.Equal("./...", plan.Arguments[^1]);
    }

    [Fact]
    public void Generate_Run_Tags_DryRun_Verbose()
    {
        var plan = Go.Generate(FakeTool(), s => s
            .SetRunFilter("mockgen").AddTag("tools").SetDryRun().SetVerbose().AddPackage("./..."));
        Assert.Equal("generate", plan.Arguments[0]);
        Assert.Equal("mockgen", plan.Arguments[IndexOf(plan.Arguments, "-run") + 1]);
        Assert.Equal("tools", plan.Arguments[IndexOf(plan.Arguments, "-tags") + 1]);
        Assert.Contains("-n", plan.Arguments);
        Assert.Contains("-v", plan.Arguments);
        Assert.Equal("./...", plan.Arguments[^1]);
    }

    [Fact]
    public void Fmt_DryRun_AllPackages()
    {
        var plan = Go.Fmt(FakeTool(), s => s.SetDryRun().AllPackages());
        Assert.Equal("fmt", plan.Arguments[0]);
        Assert.Contains("-n", plan.Arguments);
        Assert.Equal("./...", plan.Arguments[^1]);
    }

    [Fact]
    public void Clean_All_Cache_Flags()
    {
        var plan = Go.Clean(FakeTool(), s => s
            .SetCache().SetTestCache().SetModCache().SetFuzzCache().AddPackage("./..."));
        Assert.Equal("clean", plan.Arguments[0]);
        Assert.Contains("-cache", plan.Arguments);
        Assert.Contains("-testcache", plan.Arguments);
        Assert.Contains("-modcache", plan.Arguments);
        Assert.Contains("-fuzzcache", plan.Arguments);
        Assert.Equal("./...", plan.Arguments[^1]);
    }

    [Fact]
    public void Env_Json_And_Names()
    {
        var plan = Go.Env(FakeTool(), s => s.SetJson().AddName("GOPATH"));
        Assert.Equal("env", plan.Arguments[0]);
        Assert.Contains("-json", plan.Arguments);
        Assert.Equal("GOPATH", plan.Arguments[^1]);
    }

    [Fact]
    public void Version_Modules_And_Files()
    {
        var plan = Go.Version(FakeTool(), s => s.SetModules().AddFile("./bin/app"));
        Assert.Equal("version", plan.Arguments[0]);
        Assert.Contains("-m", plan.Arguments);
        Assert.Equal("./bin/app", plan.Arguments[^1]);
    }

    [Fact]
    public void Mod_Verify_TwoTokenVerb()
    {
        var plan = Go.Mod.Verify(FakeTool());
        Assert.Equal(new[] { "mod", "verify" }, plan.Arguments);
    }

    [Fact]
    public void Mod_Tidy_Go_Compat_PrintCommands()
    {
        var plan = Go.Mod.Tidy(FakeTool(), s => s.SetGo("1.23").SetCompat("1.22").SetPrintCommands());
        Assert.Equal("mod", plan.Arguments[0]);
        Assert.Equal("tidy", plan.Arguments[1]);
        Assert.Contains("-go=1.23", plan.Arguments);
        Assert.Contains("-compat=1.22", plan.Arguments);
        Assert.Contains("-x", plan.Arguments);
    }

    [Fact]
    public void Mod_Download_PrintCommands()
    {
        var plan = Go.Mod.Download(FakeTool(), s => s.SetPrintCommands());
        Assert.Contains("-x", plan.Arguments);
    }

    [Fact]
    public void Work_Sync_TwoTokenVerb()
    {
        var plan = Go.Work.Sync(FakeTool());
        Assert.Equal(new[] { "work", "sync" }, plan.Arguments);
    }

    // ---------- go test — the remaining knobs ----------

    [Fact]
    public void Test_All_Remaining_Flags()
    {
        var plan = Go.Test(FakeTool(), s => s
            .SetBenchFilter(".").SetBenchMem().SetCount(1).SetShort().SetTimeout("30s")
            .SetJson().SetFailFast().SetParallelTests(4).SetShuffle("on").SetVetOff()
            .SetCompileOnly().SetOutput("t.bin").AddExtraArg("-test.v"));
        Assert.Equal("test", plan.Arguments[0]);
        Assert.Equal(".", plan.Arguments[IndexOf(plan.Arguments, "-bench") + 1]);
        Assert.Contains("-benchmem", plan.Arguments);
        Assert.Equal("1", plan.Arguments[IndexOf(plan.Arguments, "-count") + 1]);
        Assert.Contains("-short", plan.Arguments);
        Assert.Equal("30s", plan.Arguments[IndexOf(plan.Arguments, "-timeout") + 1]);
        Assert.Contains("-json", plan.Arguments);
        Assert.Contains("-failfast", plan.Arguments);
        Assert.Equal("4", plan.Arguments[IndexOf(plan.Arguments, "-parallel") + 1]);
        Assert.Contains("-shuffle=on", plan.Arguments);
        Assert.Contains("-vet=off", plan.Arguments);
        Assert.Contains("-c", plan.Arguments);
        Assert.Equal("t.bin", plan.Arguments[IndexOf(plan.Arguments, "-o") + 1]);
        Assert.Equal("-test.v", plan.Arguments[^1]);
    }

    [Fact]
    public void Run_AddProgramArg_Single()
    {
        var plan = Go.Run_(FakeTool(), s => s.AddPackage("./cmd").AddProgramArg("--flag"));
        Assert.Equal("--flag", plan.Arguments[^1]);
    }

    // ---------- base-settings fluent setters ----------

    [Fact]
    public void Base_Goos_Goarch_Goarm_Env_Setters()
    {
        var plan = Go.Build(FakeTool(), s => s
            .SetGoos("linux").SetGoarch("arm").SetGoarm("7").SetEnvironmentVariable("FOO", "bar"));
        Assert.Equal("linux", plan.Environment["GOOS"]);
        Assert.Equal("arm", plan.Environment["GOARCH"]);
        Assert.Equal("7", plan.Environment["GOARM"]);
        Assert.Equal("bar", plan.Environment["FOO"]);
    }

    [Fact]
    public void BuildLike_Flag_Setters()
    {
        var plan = Go.Build(FakeTool(), s => s
            .AddTag("netgo").AddLdflag("-X a=b").AddGcflag("all=-N -l").AddAsmflag("-trimpath")
            .SetRace().SetCoverMode("atomic").AddCoverPkg("./...").SetBuildMode("pie")
            .SetParallelism(4).SetPrintCommands().AddPackages("./a", "./b"));
        Assert.Equal("netgo", plan.Arguments[IndexOf(plan.Arguments, "-tags") + 1]);
        Assert.Equal("-X a=b", plan.Arguments[IndexOf(plan.Arguments, "-ldflags") + 1]);
        Assert.Equal("all=-N -l", plan.Arguments[IndexOf(plan.Arguments, "-gcflags") + 1]);
        Assert.Equal("-trimpath", plan.Arguments[IndexOf(plan.Arguments, "-asmflags") + 1]);
        Assert.Contains("-race", plan.Arguments);
        Assert.Equal("atomic", plan.Arguments[IndexOf(plan.Arguments, "-covermode") + 1]);
        Assert.Equal("./...", plan.Arguments[IndexOf(plan.Arguments, "-coverpkg") + 1]);
        Assert.Contains("-buildmode=pie", plan.Arguments);
        Assert.Equal("4", plan.Arguments[IndexOf(plan.Arguments, "-p") + 1]);
        Assert.Contains("-x", plan.Arguments);
        Assert.Equal("./a", plan.Arguments[^2]);
        Assert.Equal("./b", plan.Arguments[^1]);
    }

    [Fact]
    public void BuildLike_Msan_Asan_And_ModModes()
    {
        Assert.Contains("-msan", Go.Build(FakeTool(), s => s.SetMsan()).Arguments);
        Assert.Contains("-asan", Go.Build(FakeTool(), s => s.SetAsan()).Arguments);
        Assert.Contains("-mod=mod", Go.Build(FakeTool(), s => s.SetModMode("mod")).Arguments);
        Assert.Contains("-mod=vendor", Go.Build(FakeTool(), s => s.SetVendor()).Arguments);
    }

    // ---------- object-init overloads ----------

    [Fact]
    public void ObjectInit_Overloads_Produce_Plans()
    {
        var tool = FakeTool();
        Assert.Equal("build", Go.Build(tool, new GoBuildSettings { Output = "out" }).Arguments[0]);
        Assert.Equal("test", Go.Test(tool, new GoTestSettings()).Arguments[0]);
        Assert.Equal("vet", Go.Vet(tool, new GoVetSettings()).Arguments[0]);
        Assert.Equal("run", Go.Run_(tool, new GoRunSettings()).Arguments[0]);
        Assert.Equal("install", Go.Install(tool, new GoInstallSettings()).Arguments[0]);
        Assert.Equal("list", Go.List(tool, new GoListSettings()).Arguments[0]);
        Assert.Equal("generate", Go.Generate(tool, new GoGenerateSettings()).Arguments[0]);
        Assert.Equal("fmt", Go.Fmt(tool, new GoFmtSettings()).Arguments[0]);
        Assert.Equal("clean", Go.Clean(tool, new GoCleanSettings()).Arguments[0]);
    }

    // ---------- go.mod helpers (AbsolutePath surface) ----------

    [Fact]
    public void GetModulePath_And_GoDirective_FromFile()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var p = Path.Combine(dir.FullName, "go.mod");
            File.WriteAllText(p, "module example.com/foo/bar\n\ngo 1.23.4\n\nrequire x v1.0.0\n");
            var ap = AbsolutePath.Create(p);
            Assert.Equal("example.com/foo/bar", Go.GetModulePath(ap));
            Assert.Equal("1.23.4", Go.GetGoDirective(ap));
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public void Fmt_AddPackage_Positional()
    {
        var plan = Go.Fmt(FakeTool(), s => s.AddPackage("./pkg"));
        Assert.Equal("fmt", plan.Arguments[0]);
        Assert.Equal("./pkg", plan.Arguments[^1]);
    }

    [Fact]
    public void SetVersionVariable_EmptyName_Throws()
    {
        Assert.Throws<System.ArgumentException>(() =>
            Go.Build(FakeTool(), s => s.SetVersionVariable("  ", "1.0.0")));
    }

    [Fact]
    public void GetModulePath_MissingFile_ReturnsNull()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var ap = AbsolutePath.Create(Path.Combine(dir.FullName, "nope.mod"));
            Assert.Null(Go.GetModulePath(ap));
            Assert.Null(Go.GetGoDirective(ap));
        }
        finally { dir.Delete(recursive: true); }
    }
}
