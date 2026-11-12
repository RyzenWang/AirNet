# Changelog

All notable changes are documented here. The format follows Keep a Changelog and the
project uses semantic versioning for release tags.

## [Unreleased]

### Added

- Upload bandwidth measurement. The speed page now times both directions: the
  downlink is bounded by a 4s budget rather than a byte count, and the uplink
  streams 4MB from a single buffer. Either direction can fail on its own and the
  page says "未测出" instead of writing a fake number.
- IPv4/IPv6 dual-stack comparison. The same host is timed with five TCP
  handshakes per family, in parallel with the first-byte breakdown, and reported
  as 均衡 / IPv6 更快 / IPv6 偏慢. A slow IPv6 path now raises a diagnosis line,
  because most stacks try IPv6 first and the fallback delay lands in the first
  byte. Thresholds (0.9 / 1.3) are unit-tested, and a host without IPv6 is
  reported as "仅 IPv4" without affecting the score.
- `--speedtest <path>` for a headless both-directions run.
- Dual-stack line in the HTML report and the path page summary.

### Fixed

- Downlink measurement reported 0 Mbps on slow links: the old probe required
  12MB inside an 8s timeout, so anything under ~12 Mbps timed out. It is now
  time-bounded and stops reading when the budget expires.

### Verified

- Wi-Fi radio sampling confirmed on a real Windows wireless adapter (2026-11-12):
  SSID, signal quality, negotiated rate and PHY generation all come through
  `wlanapi`. This closes the last "written but never run on hardware" path in the
  probe layer.
- Still outstanding on that path: dBm is an estimate (`quality / 2 - 100`) rather
  than a scanned RSSI, neighbour/channel advice is not implemented, and the macOS
  (CoreWLAN) and Linux (`/proc/net/wireless`) radio branches do not exist — those
  platforms report "wireless" with no numbers.

## [0.2.3] - 2026-11-12

### Changed

- Light theme now follows AirPDF's terracotta accent (`#E3563B`) for brand,
  primary actions, navigation selection and busy motion. Good status, score rings,
  charts, and latency segment bars stay on the teal semantic colour; dark theme is
  unchanged.
- Version and footer now report 0.2.3; all screenshots regenerated for the A theme.

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
