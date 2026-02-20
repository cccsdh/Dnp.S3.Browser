# Dnp.S3.Browser

A cross-platform .NET MAUI S3 browser utility.  Allows listing S3 buckets, browsing bucket contents by drilling into folders, and performing download, upload, rename and delete operations.  A local filesystem-backed service is available for development/testing.

Highlights
- .NET MAUI UI (`Dnp.S3.Browser.UI`) targeting .NET 10
- `Dnp.S3.Browser.Core` contains models and the `IS3Service` interface
- `Dnp.S3.Browser.Services` contains `AwsS3Service` (AWSSDK.S3) and `LocalS3Service` (filesystem) implementations
- `Dnp.S3.Browser.ViewModels` contains `S3BrowserViewModel`

Prerequisites
- .NET 10 SDK
- For development on Windows: Visual Studio 2022/2023 with the .NET MAUI workload installed (or the equivalent MAUI workload via `dotnet workload install maui`)
- (Optional) AWS credentials for accessing real S3 buckets

Configuration
- The UI project includes an `appsettings.json` used to choose the service and (optionally) provide test AWS credentials.
  - `UseLocalS3` (bool) — when `true`, uses `LocalS3Service` (filesystem); when `false` (default) uses AWS.
  - `AWS:Region` — AWS region to use (e.g. `us-east-1`).
  - `AWS:AccessKey`, `AWS:SecretKey` — optional testing credentials. Do NOT commit secrets to source control in production. Remove these values before publishing to let the AWS SDK use the default credential chain.

Local filesystem testing
- The `LocalS3Service` stores buckets as directories under the application's data directory (e.g. `%APPDATA%/LocalS3` on Windows). Create a folder with the bucket name and populate files/folders to test browsing.

Run locally
- Restore and run from the solution root:

```bash
# restore
dotnet restore

# run the MAUI app (platform-specific targets required)
dotnet build Dnp.S3.Browser.UI/Dnp.S3.Browser.UI.csproj -c Debug
# To run from Visual Studio, open the solution and choose the desired platform (Windows, MacCatalyst, etc.)
```

Packaging for Windows (CI will produce a publish artifact)
- The included GitHub Actions workflow (`.github/workflows/ci-windows.yml`) will build, test and publish a Windows artifact. It zips the `dotnet publish` output and stores it as a workflow artifact.

Security
- Do not keep `AWS:AccessKey` and `AWS:SecretKey` in `appsettings.json` for published builds. Use environment variables, AWS profiles, or instance/IAM roles in production.

Extending
- Add a tests project and the CI will run `dotnet test` automatically.
- Improve caching or change cache provider by replacing the `IMemoryCache` registration in `MauiProgram.cs`.

License
- This repository contains custom code; check project-level license if included.
