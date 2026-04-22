# Fortyk.Tooling.Tailwind

MSBuild integration for [Tailwind CSS](https://tailwindcss.com/) CLI in .NET projects. Automatically downloads and runs the Tailwind CSS standalone CLI during build, with no Node.js or npm required.

## Features

- **Automatic CLI download**: Downloads the correct Tailwind CSS CLI binary for your OS on first build
- **Version-stamped binaries**: The downloaded CLI binary includes the version number in its filename (e.g., `tailwindcss-4.2.1`), allowing multiple versions to coexist in shared directories
- **Build integration**: Processes your CSS files with Tailwind during build (with minification in Release mode)
- **Watch mode**: Supports Tailwind watch mode for development
- **Scoped CSS support**: Automatically processes Blazor scoped CSS files

## Installation

Install the NuGet package:

```xml
<PackageReference Include="Fortyk.Tooling.Tailwind" Version="x.y.z" />
```

When installed as a NuGet package, props and targets are imported automatically by MSBuild.

## Configuration

All properties can be overridden in the consuming project (or in a `Directory.Build.props`).

| Property | Default | Description |
|---|---|---|
| `TailwindToolsVersion` | `4.2.3` | Version of the Tailwind CSS CLI to download |
| `TailwindInputCss` | `Styles/tailwind.css` | Input CSS file path (relative to `TailwindWorkingDirectory`) |
| `TailwindOutputCss` | `wwwroot/css/app.min.css` | Output CSS file path (relative to `TailwindWorkingDirectory`) |
| `TailwindWorkingDirectory` | `$(MSBuildProjectDirectory)` | Working directory for Tailwind CLI execution |
| `TailwindExeDir` | `$(TailwindWorkingDirectory)/.tailwind` | Directory where the CLI binary is stored |
| `TailwindExtraArgs` | *(empty)* | Extra arguments to pass to the Tailwind CLI |

### Example: Custom Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <!-- Override Tailwind CSS version -->
    <TailwindToolsVersion>4.3.0</TailwindToolsVersion>
    <!-- Custom input/output paths -->
    <TailwindInputCss>Styles/main.css</TailwindInputCss>
    <TailwindOutputCss>wwwroot/css/site.min.css</TailwindOutputCss>
    <!-- Store CLI binary in a shared location -->
    <TailwindExeDir>$(SolutionDir).tailwind</TailwindExeDir>
  </PropertyGroup>

  <PackageReference Include="Fortyk.Tooling.Tailwind" Version="1.0.0" />

</Project>
```

## MSBuild Targets

| Target | Description |
|---|---|
| `ProcessCssWithTailwindOnBuild` | Runs before build. Processes CSS with Tailwind (minified in Release). |
| `TailwindWatch` | Starts Tailwind in watch mode for live CSS updates during development. |
| `ProcessScopedCssWithTailwindOnSave` | Processes Blazor scoped CSS files after they are generated. |

## Non-Standard File Extensions (e.g., `.razor`)

By default, Tailwind CSS v4 automatically detects utility classes in common file types (`.html`, `.js`, `.ts`, `.jsx`, `.tsx`, etc.). For non-standard file extensions like `.razor` (Blazor), `.vue`, or `.svelte`, you need to explicitly tell Tailwind where to look for classes.

### Using `@source` (Recommended)

Add `@source` directives in your input CSS file (e.g., `Styles/tailwind.css`) to specify additional file paths and extensions:

```css
@import "tailwindcss";

/* Scan all .razor files in the project */
@source "../**/*.razor";
```

Multiple `@source` directives can be combined:

```css
@import "tailwindcss";

@source "../Components/**/*.razor";
@source "../Pages/**/*.razor";
@source "../Shared/**/*.razor";
```

### Using `tailwind.config.js`

For more complex configurations, you can use a JavaScript config file. Create a `tailwind.config.js` in your project root:

```js
/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './Pages/**/*.razor',
    './Components/**/*.razor',
    './Shared/**/*.razor',
    './wwwroot/**/*.html',
  ],
}
```

Then reference it in your input CSS file using the `@config` directive:

```css
@import "tailwindcss";
@config "../tailwind.config.js";
```

> **Note:** In Tailwind CSS v4, configuration is CSS-first. The `@config` directive loads a JavaScript config file for advanced scenarios such as custom plugins or complex content paths. For simple file extension additions, `@source` is preferred.

### Full Blazor Example

Here is a complete example for a Blazor project:

**`Styles/tailwind.css`**:
```css
@import "tailwindcss";
@source "../**/*.razor";
```

**Project file (`.csproj`)**:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Fortyk.Tooling.Tailwind" Version="1.0.0" />
  </ItemGroup>

</Project>
```

With this setup, Tailwind automatically scans all `.razor` files for utility classes during build.

### Watch Mode

To start Tailwind in watch mode:

```bash
dotnet msbuild YourProject -t:TailwindWatch
```

## How It Works

1. **On first build**, the CLI binary is downloaded from the [Tailwind CSS GitHub releases](https://github.com/tailwindlabs/tailwindcss/releases) to the `TailwindExeDir` directory
2. The binary is named with a version suffix (e.g., `tailwindcss-4.2.1` or `tailwindcss-4.2.1.exe` on Windows), so different projects can use different Tailwind versions without conflicts
3. On subsequent builds, the download is skipped if the versioned binary already exists
4. During build, the CLI processes the input CSS and generates the output CSS
5. In Release configuration, the `--minify` flag is automatically applied

## Notes

- The `.tailwind/` directory (where CLI binaries are stored) should be added to `.gitignore`
- No Node.js or npm is required — the standalone Tailwind CSS CLI is used directly
- On Linux/macOS, the binary is automatically made executable after download

## Disclaimer

This package is an independent, community-driven project and is **not** affiliated with, endorsed by, or officially related to [Tailwind Labs Inc.](https://tailwindlabs.com/) or the Tailwind CSS brand. "Tailwind CSS" is a trademark of Tailwind Labs Inc. This package simply provides MSBuild tooling to download and invoke the official Tailwind CSS standalone CLI.

## License

This package is licensed under the [MIT License](https://opensource.org/licenses/MIT).
