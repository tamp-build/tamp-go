using Tamp.Go;
using Xunit;

namespace Tamp.Go.Tests;

public sealed class GoModTests
{
    [Fact]
    public void GetModulePath_SingleLine()
    {
        var content = "module example.com/foo/bar\n\ngo 1.23\n";
        Assert.Equal("example.com/foo/bar", GoMod.GetModulePath(content));
    }

    [Fact]
    public void GetModulePath_IgnoresComment()
    {
        var content = "module example.com/foo // legacy path\n";
        Assert.Equal("example.com/foo", GoMod.GetModulePath(content));
    }

    [Fact]
    public void GetModulePath_Quoted()
    {
        var content = "module \"example.com/foo\"\n";
        Assert.Equal("example.com/foo", GoMod.GetModulePath(content));
    }

    [Fact]
    public void GetModulePath_Missing_ReturnsNull()
    {
        Assert.Null(GoMod.GetModulePath("go 1.23\n"));
    }

    [Fact]
    public void GetGoDirective_ReadsVersion()
    {
        var content = "module example.com/foo\n\ngo 1.23.4\n";
        Assert.Equal("1.23.4", GoMod.GetGoDirective(content));
    }

    [Fact]
    public void GetGoDirective_DoesNotMatchModulePathStartingWithGo()
    {
        // `module example.com/go-thing` must not be read as a go directive.
        var content = "module example.com/go-thing\n";
        Assert.Null(GoMod.GetGoDirective(content));
    }

    [Fact]
    public void GetGoDirective_Missing_ReturnsNull()
    {
        Assert.Null(GoMod.GetGoDirective("module example.com/foo\n"));
    }
}
