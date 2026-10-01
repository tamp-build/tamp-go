using System.Collections.Generic;
using Tamp;
using Tamp.Go;
using Xunit;

namespace Tamp.Go.Tests;

public sealed class GoTests
{
    private static Tool FakeTool() => new(AbsolutePath.Create("/fake/go"));

    private static int IndexOf(IReadOnlyList<string> args, string token)
    {
        for (var i = 0; i < args.Count; i++) if (args[i] == token) return i;
        return -1;
    }

    // ---- Build ----

    [Fact]
    public void Build_Bare_Has_Verb()
    {
        var plan = Go.Build(FakeTool());
        Assert.Equal("build", plan.Arguments[0]);
    }

    [Fact]
    public void Build_Output_Comes_Before_Flags()
    {
        var plan = Go.Build(FakeTool(), s => s.SetOutput("artifacts/server").SetTrimpath());
        Assert.Equal("build", plan.Arguments[0]);
        Assert.Equal("-o", plan.Arguments[1]);
        Assert.Equal("artifacts/server", plan.Arguments[2]);
        Assert.Contains("-trimpath", plan.Arguments);
    }

    [Fact]
    public void Build_Packages_Are_Trailing_Positionals()
    {
        var plan = Go.Build(FakeTool(), s => s.SetVerbose().AllPackages());
        Assert.Equal("./...", plan.Arguments[^1]);
    }

    [Fact]
    public void Build_Tags_Are_Comma_Joined()
    {
        var plan = Go.Build(FakeTool(), s => s.AddTags("netgo", "osusergo"));
        Assert.Equal("netgo,osusergo", plan.Arguments[IndexOf(plan.Arguments, "-tags") + 1]);
    }

    // ---- Cross-compilation goes to the ENVIRONMENT, not args ----

    [Fact]
    public void CrossCompile_Emits_Goos_Goarch_Cgo_To_Environment()
    {
        var plan = Go.Build(FakeTool(), s => s
            .SetPlatform("linux", "arm64")
            .SetCgoEnabled(false));
        Assert.Equal("linux", plan.Environment["GOOS"]);
        Assert.Equal("arm64", plan.Environment["GOARCH"]);
        Assert.Equal("0", plan.Environment["CGO_ENABLED"]);
        // And NOT as CLI flags:
        Assert.DoesNotContain("-target", plan.Arguments);
        Assert.DoesNotContain("linux", plan.Arguments);
    }

    [Fact]
    public void CgoEnabled_True_Emits_One()
    {
        var plan = Go.Build(FakeTool(), s => s.SetCgoEnabled(true));
        Assert.Equal("1", plan.Environment["CGO_ENABLED"]);
    }

    // ---- Version stamping via ldflags ----

    [Fact]
    public void SetVersionVariable_Builds_Single_Ldflags_Argument()
    {
        var plan = Go.Build(FakeTool(), s => s
            .SetVersionVariable("main.version", "1.4.0")
            .StripDebugInfo());
        var li = IndexOf(plan.Arguments, "-ldflags");
        Assert.True(li >= 0);
        Assert.Equal("-X main.version=1.4.0 -s -w", plan.Arguments[li + 1]);
    }

    // ---- Test ----

    [Fact]
    public void Test_Cover_And_Run_Filter()
    {
        var plan = Go.Test(FakeTool(), s => s
            .SetCover()
            .SetCoverProfile("coverage.out")
            .SetRunFilter("TestFoo")
            .AllPackages());
        Assert.Equal("test", plan.Arguments[0]);
        Assert.Contains("-cover", plan.Arguments);
        Assert.Equal("coverage.out", plan.Arguments[IndexOf(plan.Arguments, "-coverprofile") + 1]);
        Assert.Equal("TestFoo", plan.Arguments[IndexOf(plan.Arguments, "-run") + 1]);
        Assert.Equal("./...", plan.Arguments[^1]);
    }

    [Fact]
    public void Test_ExtraArgs_Come_After_Packages_With_Args_Separator()
    {
        var plan = Go.Test(FakeTool(), s => s
            .AddPackage("./pkg")
            .AddExtraArgs("-test.v"));
        var ai = IndexOf(plan.Arguments, "-args");
        var pi = IndexOf(plan.Arguments, "./pkg");
        Assert.True(pi < ai, "package pattern must precede -args");
        Assert.Equal("-test.v", plan.Arguments[^1]);
    }

    // ---- Run forwards program args after the package ----

    [Fact]
    public void Run_Forwards_Program_Args_After_Package()
    {
        var plan = Go.Run_(FakeTool(), s => s
            .AddPackage("./cmd/tool")
            .AddProgramArgs("--flag", "value"));
        Assert.Equal("run", plan.Arguments[0]);
        var pi = IndexOf(plan.Arguments, "./cmd/tool");
        Assert.Equal("--flag", plan.Arguments[pi + 1]);
        Assert.Equal("value", plan.Arguments[pi + 2]);
    }

    // ---- mod sub-facade ----

    [Fact]
    public void Mod_Tidy_Emits_Two_Token_Verb()
    {
        var plan = Go.Mod.Tidy(FakeTool(), s => s.SetGo("1.23"));
        Assert.Equal("mod", plan.Arguments[0]);
        Assert.Equal("tidy", plan.Arguments[1]);
        Assert.Contains("-go=1.23", plan.Arguments);
    }

    [Fact]
    public void Mod_Download_Modules_Are_Positional()
    {
        var plan = Go.Mod.Download(FakeTool(), s => s.SetJson().AddModule("example.com/foo"));
        Assert.Equal("mod", plan.Arguments[0]);
        Assert.Equal("download", plan.Arguments[1]);
        Assert.Contains("-json", plan.Arguments);
        Assert.Equal("example.com/foo", plan.Arguments[^1]);
    }

    // ---- Vet / mod modes ----

    [Fact]
    public void Vet_With_ModReadonly()
    {
        var plan = Go.Vet(FakeTool(), s => s.SetReadonly().AllPackages());
        Assert.Equal("vet", plan.Arguments[0]);
        Assert.Contains("-mod=readonly", plan.Arguments);
    }

    // ---- Raw escape hatch ----

    [Fact]
    public void Raw_Passes_Through()
    {
        var plan = Go.Raw(FakeTool(), "tool", "cover", "-func=coverage.out");
        Assert.Equal(new[] { "tool", "cover", "-func=coverage.out" }, plan.Arguments);
    }

    [Fact]
    public void Raw_Empty_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => Go.Raw(FakeTool()));
    }

    // ---- WorkingDirectory passthrough ----

    [Fact]
    public void WorkingDirectory_Flows_To_Plan()
    {
        var plan = Go.Build(FakeTool(), s => s.SetWorkingDirectory("/repo/svc"));
        Assert.Equal("/repo/svc", plan.WorkingDirectory);
    }
}
