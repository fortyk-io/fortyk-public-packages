namespace Fortyk.Tooling.Tailwind.IntegrationTests;

/// <summary>
/// End-to-end tests: each test builds a throwaway project with the tooling imported from source,
/// varying one MSBuild property at a time, and checks the files on disk and the build log.
/// </summary>
public sealed class TailwindFlagsTests(TailwindBuildFixture fixture) : IClassFixture<TailwindBuildFixture>
{
    [Fact]
    public void Defaults_generate_both_css_files_under_wwwroot()
    {
        var project = fixture.CreateProject("defaults");

        var result = fixture.Build(project);

        AssertSucceeded(result);
        var css = project.PathOf("wwwroot", "css", "app.css");
        var minified = project.PathOf("wwwroot", "css", "app.min.css");
        Assert.True(File.Exists(css), "Expected non-minified output at " + css);
        Assert.True(File.Exists(minified), "Expected minified output at " + minified);

        var cssText = File.ReadAllText(css);
        Assert.Contains(".text-3xl", cssText, StringComparison.Ordinal);
        Assert.Contains(".font-bold", cssText, StringComparison.Ordinal);
        Assert.Contains('\n', cssText.TrimEnd());
        Assert.True(new FileInfo(minified).Length < new FileInfo(css).Length, "Minified output should be smaller");
    }

    [Fact]
    public void Build_log_reports_version_and_cached_binary_path()
    {
        var project = fixture.CreateProject("log");

        var result = fixture.Build(project);

        AssertSucceeded(result);
        Assert.Contains("Tailwind CLI already present at " + fixture.SharedExePath, result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Downloading Tailwind CLI", result.Output, StringComparison.Ordinal);
        Assert.Contains("v" + fixture.DefaultVersion, result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TailwindGenerateMinified_false_skips_minified_output()
    {
        var project = fixture.CreateProject("no-minified");

        var result = fixture.Build(project, ("TailwindGenerateMinified", "false"));

        AssertSucceeded(result);
        Assert.True(File.Exists(project.PathOf("wwwroot", "css", "app.css")));
        Assert.False(File.Exists(project.PathOf("wwwroot", "css", "app.min.css")));
        Assert.DoesNotContain("minified output: wwwroot/css/app.min.css", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void TailwindGenerateUnminified_false_skips_unminified_output()
    {
        var project = fixture.CreateProject("no-unminified");

        var result = fixture.Build(project, ("TailwindGenerateUnminified", "false"));

        AssertSucceeded(result);
        Assert.False(File.Exists(project.PathOf("wwwroot", "css", "app.css")));
        Assert.True(File.Exists(project.PathOf("wwwroot", "css", "app.min.css")));
        Assert.DoesNotContain("non-minified output: wwwroot/css/app.css", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_generate_flags_false_runs_no_tailwind_and_still_builds()
    {
        var project = fixture.CreateProject("nothing");

        var result = fixture.Build(project,
            ("TailwindGenerateUnminified", "false"),
            ("TailwindGenerateMinified", "false"));

        AssertSucceeded(result);
        Assert.False(Directory.Exists(project.PathOf("wwwroot")));
        Assert.DoesNotContain("Running Tailwind CLI", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_input_and_output_paths_are_honoured()
    {
        var project = fixture.CreateProject("custom-paths");
        Directory.CreateDirectory(project.PathOf("assets"));
        File.Move(project.PathOf("Styles", "tailwind.css"), project.PathOf("assets", "site.css"));

        var result = fixture.Build(project,
            ("TailwindInputCss", "assets/site.css"),
            ("TailwindOutputCss", "dist/site.css"),
            ("TailwindOutputCssMinified", "dist/site.min.css"));

        AssertSucceeded(result);
        Assert.True(File.Exists(project.PathOf("dist", "site.css")));
        Assert.True(File.Exists(project.PathOf("dist", "site.min.css")));
        Assert.False(Directory.Exists(project.PathOf("wwwroot")));
    }

    [Fact]
    public void Missing_input_css_fails_the_build()
    {
        var project = fixture.CreateProject("missing-input");
        File.Delete(project.PathOf("Styles", "tailwind.css"));

        var result = fixture.Build(project);

        Assert.False(result.Succeeded, "Build should fail when the input CSS does not exist:\n" + result.Output);
        Assert.False(File.Exists(project.PathOf("wwwroot", "css", "app.css")));
    }

    [Fact]
    public void TailwindWorkingDirectory_resolves_relative_paths_against_it()
    {
        var project = fixture.CreateProject("working-dir");
        var web = project.PathOf("web");
        Directory.Delete(project.PathOf("Styles"), recursive: true);
        File.Delete(project.PathOf("index.html"));
        TailwindBuildFixture.WriteSources(web);

        var result = fixture.Build(project, ("TailwindWorkingDirectory", web));

        AssertSucceeded(result);
        Assert.True(File.Exists(Path.Combine(web, "wwwroot", "css", "app.css")));
        Assert.True(File.Exists(Path.Combine(web, "wwwroot", "css", "app.min.css")));
        Assert.False(Directory.Exists(project.PathOf("wwwroot")));
    }

    [Fact]
    public void Default_TailwindExeDir_is_dot_tailwind_in_project_and_skips_download_when_present()
    {
        var project = fixture.CreateProject("default-exe-dir");
        var expectedExe = project.PathOf(".tailwind", TailwindBuildFixture.ExeName(fixture.DefaultVersion));
        fixture.CopySharedExe(expectedExe);

        var result = TailwindBuildFixture.BuildWithDefaultExeDir(project);

        AssertSucceeded(result);
        Assert.Contains("Tailwind CLI already present at " + expectedExe, result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Downloading Tailwind CLI", result.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(project.PathOf("wwwroot", "css", "app.css")));
    }

    [Fact]
    public void TailwindToolsVersion_selects_a_version_stamped_binary()
    {
        const string fakeVersion = "0.0.0-test";
        var project = fixture.CreateProject("tools-version");
        var exeDir = project.PathOf("cli");
        fixture.CopySharedExe(Path.Combine(exeDir, TailwindBuildFixture.ExeName(fakeVersion)));

        var result = fixture.Build(project,
            ("TailwindToolsVersion", fakeVersion),
            ("TailwindExeDir", exeDir));

        AssertSucceeded(result);
        Assert.Contains(Path.Combine(exeDir, TailwindBuildFixture.ExeName(fakeVersion)), result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Downloading Tailwind CLI", result.Output, StringComparison.Ordinal);
        Assert.True(File.Exists(project.PathOf("wwwroot", "css", "app.css")));
    }

    [Fact]
    public void TailwindExtraArgs_are_appended_to_every_cli_invocation()
    {
        var project = fixture.CreateProject("extra-args");

        var result = fixture.Build(project, ("TailwindExtraArgs", "--optimize"));

        AssertSucceeded(result);
        var invocations = result.Output.Split('\n').Count(line =>
            line.Contains(TailwindBuildFixture.ExeName(fixture.DefaultVersion), StringComparison.Ordinal)
            && line.Contains(" -i ", StringComparison.Ordinal)
            && line.TrimEnd().EndsWith("--optimize", StringComparison.Ordinal));
        Assert.Equal(2, invocations);
    }

    private static void AssertSucceeded(BuildResult result) =>
        Assert.True(result.Succeeded, "Build failed:\n" + result.Output);
}
