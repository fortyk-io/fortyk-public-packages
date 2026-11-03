# SampleApp

The smallest possible ASP.NET Core app that uses **Fortyk.Tooling.Tailwind**.

- `Styles/tailwind.css` is the Tailwind input (just `@import "tailwindcss";`)
- `wwwroot/index.html` uses a few utility classes and links `css/app.min.css`
- `SampleApp.csproj` imports the tooling's `.props` and `.targets` directly from `src/`,
  so the sample always runs against the current source. In a real project you would
  use a `PackageReference` to `Fortyk.Tooling.Tailwind` instead.

## Run

```bash
dotnet run --project samples/SampleApp
```

On the first build the Tailwind CLI is downloaded to `samples/SampleApp/.tailwind/`,
then `wwwroot/css/app.css` and `wwwroot/css/app.min.css` are generated. Both the
binary and the generated CSS are git-ignored.
