# Changelog

All notable changes are documented here. The format follows Keep a Changelog and the
project uses semantic versioning for release tags.

## [Unreleased]

### Changed

- Light theme now follows AirPDF's terracotta accent (`#E3563B`) for brand,
  primary actions, navigation selection and busy motion. Good status, score rings,
  charts, and latency segment bars stay on the teal semantic colour; dark theme is
  unchanged.

## [0.2.2] - 2026-11-12

### Added

- Public repository baseline: security policy, contribution guide, third-party notices,
  issue/PR templates, Dependabot, and CodeQL.
- Linux `run.sh` launcher and Linux `.tar.gz` archives alongside the zip packages.
- Release/download and platform-support documentation.

### Changed

- Linux package instructions now explain how to recover executable bits after a
  permission-stripping extraction.
- Version and footer now report 0.2.2.

## [0.2.1] - 2026-11-12

### Added

- Website latency page with 11 default sites and up to 8 custom HTTPS sites.
- HTTP status classification for blocked and server-error responses.
- `sites.jsonl` history, cross-restart deltas, and the `--sites` CLI.
- Optional website table in the HTML report.
- macOS arm64/x64 and Linux x64/arm64 portable assets.

### Fixed

- Custom-site manager action buttons no longer clip their labels.
- Clean publish output no longer carries stale native libraries; Windows portable
  package is about 26.9 MB.

### Verified

- Windows build and 70 unit tests.
- Linux x64 package launched successfully in a real environment.
- macOS and Linux arm64 packages are built and archive-validated, but not runtime-tested.

## [0.1.0]

Initial prototype: six-page Avalonia shell, network probes, scoring/diagnosis, path
tracing, MTU, history, DNS helper, HTML report, and portable Windows packaging.
