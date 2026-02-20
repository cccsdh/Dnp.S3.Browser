# Dnp.S3.Browser

[![CI](https://github.com/cccsdh/Dnp.S3.Browser/actions/workflows/ci-msix.yml/badge.svg)](https://github.com/cccsdh/Dnp.S3.Browser/actions/workflows/ci-msix.yml)


A .NET MAUI S3 browser utility that lists S3 buckets, browses bucket contents, and supports download, upload, rename and delete operations. The app uses a pluggable `IS3Service` with both an AWS-backed and a local filesystem-backed implementation for testing.

This repository contains a .NET MAUI UI project at `Dnp.S3.Browser.UI` and supporting class library projects (`Dnp.S3.Browser.Core`, `Dnp.S3.Browser.Services`, `Dnp.S3.Browser.ViewModels`).

## Building locally (Windows)

Prerequisites:
- Windows 10/11 with the required developer tools installed for MAUI/WinUI packaging
- .NET 10 SDK
- (Optional for AWS) AWS credentials configured (environment or shared credentials file) or set `AWS:AccessKey` and `AWS:SecretKey` in `Dnp.S3.Browser.UI/appsettings.json` for local testing only.

To build and run the app from the command line (development):

```bash
# restore
dotnet restore
# build
dotnet build Dnp.S3.Browser.UI/Dnp.S3.Browser.UI.csproj -c Release
# run (Windows desktop)
dotnet run -p Dnp.S3.Browser.UI/Dnp.S3.Browser.UI.csproj -f net10.0-windows10.0.19041.0
```


## CI / ZIP packaging (Windows)

A GitHub Actions workflow is included that builds the solution, runs tests (if present), publishes the MAUI Windows app to a folder and produces a ZIP package with the publish output. The workflow file is `.github/workflows/ci-msix.yml`.

Notes:
- For local testing you can set `UseLocalS3` in `Dnp.S3.Browser.UI/appsettings.json` to `true` to use the local filesystem-backed S3 service.
- Do not commit real AWS secrets into source control. The `AWS:AccessKey`/`SecretKey` entries are only for temporary local testing; remove before publishing.

## Installing from ZIP

Download the ZIP artifact from the CI run artifacts or from the GitHub Release assets, extract the folder and run the executable inside (for example `Dnp.S3.Browser.UI.exe`).

Notes about signing and distribution:
- The ZIP artifact is intended for testing and internal distribution. If you need signed installers or MSIX packages for broad distribution, use a CA-signed certificate and sign the binaries or package in CI before publishing.

