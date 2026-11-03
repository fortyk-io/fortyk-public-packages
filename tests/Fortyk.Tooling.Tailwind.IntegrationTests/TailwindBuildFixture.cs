using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;

namespace Fortyk.Tooling.Tailwind.IntegrationTests;

/// <summary>
/// Shared state for the integration tests: locates the tooling's build/ folder, reads the default
/// Tailwind version from the .props file and makes sure the CLI binary has been downloaded once into
/// a shared directory so individual tests do not each pay for a ~100 MB download.
/// </summary>
public sealed class TailwindBuildFixture : IDisposable
{
    private readonly string _rootDir;

    public TailwindBuildFixture()
    {
        ToolingBuildDir = typeof(TailwindBuildFixture).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "ToolingBuildDir").Value!;

        DefaultVersion = XDocument.Load(Path.Combine(ToolingBuildDir, "Fortyk.Tooling.Tailwind.props"))
            .Descendants("TailwindToolsVersion").Single().Value;

        var testsRoot = Path.Combine(Path.GetTempPath(), "fortyk-tailwind-tests");
        SharedExeDir = Path.Combine(testsRoot, "cli");
        _rootDir = Path.Combine(testsRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);

        // Bootstrap: one build that downloads the CLI into the shared directory.
        var bootstrap = CreateProject("bootstrap");
        var result = Build(bootstrap);
        if (!result.Succeeded || !File.Exists(SharedExePath))
        {
            throw new InvalidOperationException("Bootstrap build failed to download the Tailwind CLI:\n" + result.Output);
        }
    }

    /// <summary>Absolute path to src/Fortyk.Tooling.Tailwind/build/.</summary>
    public string ToolingBuildDir { get; }

    /// <summary>The default TailwindToolsVersion declared in the .props file.</summary>
    public string DefaultVersion { get; }

    /// <summary>Directory where the CLI binary is cached across tests (passed as TailwindExeDir).</summary>
    public string SharedExeDir { get; }

    /// <summary>Path of the cached CLI binary for <see cref="DefaultVersion"/>.</summary>
    public string SharedExePath => Path.Combine(SharedExeDir, ExeName(DefaultVersion));

    /// <summary>Name the tooling gives the downloaded binary for a given version on this OS.</summary>
    public static string ExeName(string version) =>
        "tailwindcss-" + version + (OperatingSystem.IsWindows() ? ".exe" : string.Empty);

    /// <summary>
    /// Creates a minimal class library in a fresh directory that imports the tooling's .props and
    /// .targets directly from source, with a Tailwind input at Styles/tailwind.css and an HTML file
    /// using a couple of utility classes so the generated CSS has recognisable content.
    /// </summary>
    public SampleProject CreateProject(string name)
    {
        var dir = Path.Combine(_rootDir, name);
        Directory.CreateDirectory(dir);

        var csproj = Path.Combine(dir, name + ".csproj");
        File.WriteAllText(csproj, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <Import Project="{Path.Combine(ToolingBuildDir, "Fortyk.Tooling.Tailwind.props")}" />
              <Import Project="{Path.Combine(ToolingBuildDir, "Fortyk.Tooling.Tailwind.targets")}" />
            </Project>
            """);

        WriteSources(dir);
        return new SampleProject(dir, csproj);
    }

    /// <summary>Writes Styles/tailwind.css and index.html into <paramref name="dir"/>.</summary>
    public static void WriteSources(string dir)
    {
        Directory.CreateDirectory(Path.Combine(dir, "Styles"));
        File.WriteAllText(Path.Combine(dir, "Styles", "tailwind.css"), "@import \"tailwindcss\";\n");
        File.WriteAllText(Path.Combine(dir, "index.html"),
            "<div class=\"text-3xl font-bold underline\">Hello</div>\n");
    }

    /// <summary>
    /// Runs <c>dotnet build</c> on the project. Unless the caller sets TailwindExeDir, the shared CLI
    /// directory is used so no download happens. Extra MSBuild properties are passed as -p:Name=Value.
    /// </summary>
    public BuildResult Build(SampleProject project, params (string Name, string Value)[] properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (properties.Any(p => p.Name == "TailwindExeDir"))
        {
            return BuildWithDefaultExeDir(project, properties);
        }

        return BuildWithDefaultExeDir(project, [("TailwindExeDir", SharedExeDir), .. properties]);
    }

    /// <summary>
    /// Runs <c>dotnet build</c> without passing TailwindExeDir at all, so the .props default
    /// ($(TailwindWorkingDirectory)/.tailwind) applies. A global property cannot be "unset" to an empty
    /// value, which is why this is a separate entry point rather than an empty-string override.
    /// </summary>
    public static BuildResult BuildWithDefaultExeDir(SampleProject project, params (string Name, string Value)[] properties)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(properties);

        var args = new List<string>
        {
            "build", project.CsprojPath, "-nologo", "-tl:off", "-v:n", "-p:UseSharedCompilation=false",
        };

        foreach (var (name, value) in properties)
        {
            args.Add($"-p:{name}={value}");
        }

        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = project.Directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        psi.Environment["DOTNET_NOLOGO"] = "1";

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return new BuildResult(process.ExitCode, stdout.Result + stderr.Result);
    }

    /// <summary>Copies the cached CLI binary to <paramref name="destination"/>, keeping it executable.</summary>
    public void CopySharedExe(string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(SharedExePath, destination, overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(destination, File.GetUnixFileMode(SharedExePath) | UnixFileMode.UserExecute);
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_rootDir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a lingering build server handle must not fail the test run.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public sealed record SampleProject(string Directory, string CsprojPath)
{
    public string PathOf(params string[] segments) => Path.Combine([Directory, .. segments]);
}

public sealed record BuildResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}
