# Dnp.S3.Browser

[![CI](https://github.com/cccsdh/Dnp.S3.Browser/actions/workflows/ci-msix.yml/badge.svg)](https://github.com/cccsdh/Dnp.S3.Browser/actions/workflows/ci-msix.yml)

A .NET MAUI (.NET 10) S3 browser UI for working with S3-compatible storage. The UI project is Dnp.S3.Browser.UI and supporting libraries provide services and viewmodels.

Highlights
- Supports AWS S3 and a LocalS3 filesystem-backed implementation (controlled with UseLocalS3 in appsettings).
- MFA support: when explicit AWS credentials (AWS:AccessKey and AWS:SecretKey) are provided and an MFA device ARN (AWS:MFA) is set, the app will prompt for an MFA code and exchange it for temporary session credentials using STS. On Windows a native dialog is used; on other platforms an inline popup is shown.
- Filtering: the Objects pane provides an overlaid filter prompt ("[Enter Filter Text]") — start typing to filter results in real time.
- Multi-select and batch operations: select multiple objects to download or delete several items at once.
- Multi-file upload: select multiple local files for upload into the selected bucket/prefix.
- UI: compact icon-only action buttons with hover tooltips and visual separators to emulate grid lines.

Configuration (Dnp.S3.Browser.UI/appsettings.json)
- UseLocalS3 (bool): true to use the local filesystem-backed S3 service for testing.
- AWS:AccessKey (string): optional AWS access key (only used if provided).
- AWS:SecretKey (string): optional AWS secret key.
- AWS:MFA (string): ARN of the MFA device. If present and explicit credentials are provided the app will prompt for MFA.
- AWS:Region (string): AWS region system name (e.g. "us-east-1").

Features in detail
- Filtering
  - The Objects list includes a filter control with an overlaid prompt. The overlay hides when the control is focused or when the user types; filtering occurs as you type.

- Multi-object download
  - Select multiple files (CTRL/Shift-select or platform selection gestures) and use the Download action to retrieve many files at once. On Windows you can choose a target folder; on other platforms the app uses the app data Downloads folder.

- Multi-object delete
  - Select multiple items and confirm deletion.

- Multi-file upload
  - Use the Upload action to select and upload many local files in a single operation.

Building & running
- Prerequisites: .NET 10 SDK, MAUI workloads installed. Visual Studio 2026 with MAUI support recommended.
- Open the solution in Visual Studio and run the Dnp.S3.Browser.UI project.

Security notes
- Do NOT commit real AWS secrets into source control. If you use AWS:AccessKey/AWS:SecretKey for local testing, remove them before committing.

Contributing
- Contributions are welcome. Open issues or pull requests against the repository.

