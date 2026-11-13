# Changelog

All notable changes are documented here. The format follows Keep a Changelog and the
project uses semantic versioning for release tags.

## [0.2.5] - 2026-11-13

### Added

- DNS three-way comparison. The settings page no longer guesses that a public resolver
  would be faster: the same hostname is asked of the current resolver, 223.5.5.5 and
  1.1.1.1 in the same run, and the recommendation quotes the three times. A change is
  only suggested when an alternative is at least 20% and 5 ms faster; when both public
  resolvers stay silent the advisor falls back to the single-sample heuristic.
- `--probe --json <path>`: one scan, one JSON document with a versioned schema
  (`schemaVersion: 1`) covering score, latency, timing segments, dual stack,
  interface, interference, DNS and findings. The exit code carries the verdict —
  0 healthy (75+), 1 degraded (60-74), 2 poor, 3 the scan itself failed — so a script
  can gate on it without parsing anything.
- Per-run deltas on the overview history. Each row now shows how it differs from the
  previous run (score, latency, jitter, downlink), tinted for good or bad, which makes
  "did replacing the router help" answerable from the first screen.

## [0.2.4] - 2026-11-12

### Added

- Upload bandwidth measurement. The speed page now times both directions: the
  downlink is bounded by a 4s budget rather than a byte count, and the uplink
  streams 4MB from a single buffer. Either direction can fail on its own and the
  page says "未测出" instead of writing a fake number.
- IPv4/IPv6 dual-stack comparison. The same host is timed with five TCP handshakes
  per family, in parallel with the first-byte breakdown, and reported as 均衡 /
  IPv6 更快 / IPv6 偏慢. A slow IPv6 path raises a diagnosis line, because most
  stacks try IPv6 first and the fallback delay lands in the first byte. Thresholds
  (0.9 / 1.3) are unit-tested; a host without IPv6 is reported as "仅 IPv4" and
  never affects the score.
- Proxy and VPN detection. The scan reports when measurements pass through a system
  proxy or a tunnel adapter, because the two distort different things: a proxy only
  affects the HTTP(S) probes (bandwidth, egress, site latency), while a tunnel
  carries everything including raw TCP timing. The distinction is in the wording,
  not just the badge — a proxy is one Info line, a VPN is a Warn.
- Upload/downlink asymmetry rule. When the downlink is fast but the uplink is under
  10 Mbps, the diagnosis names which activities suffer first: meetings, cloud
  backup and large uploads.
- `--speedtest <path>` for a headless both-directions run, `interference=` in
  `--selftest`, and a 经代理 badge where the numbers are shown.

### Fixed

- The executable icon still had the old teal background (`#2E6F63`) after the v0.2.3
  move to warm orange. Explorer and the taskbar showed a green tile next to orange
  windows. Both icons (`.ico` for Windows, `.icns` for macOS) are now generated from
  the 512px pulse shape by `docs/icon-concepts/make-icon.py` with the brand colour as
  the only input, so the next palette change cannot leave them behind.
- Site latency and the first-byte breakdown bypassed the system proxy. The probe used
  a bare socket, so on a proxied machine it reported numbers for a route the browser
  never takes — and the interference note claimed the opposite. The probe now opens
  an HTTP CONNECT tunnel through the configured proxy, counts that hop into the TCP
  segment (which is where the path is actually built), and marks the result: the
  sites page shows a 经代理 badge and `--sites` prints `viaProxy=`.
- Downlink measurement reported 0 Mbps on slow links: the old probe required 12MB
  inside an 8s timeout, so anything under ~12 Mbps timed out. It is now time-bounded
  and stops reading when the budget expires. A connection that drops mid-transfer
  reports the speed of the bytes that did arrive instead of throwing.

### Verified

- Wi-Fi radio sampling confirmed on a real Windows wireless adapter (2026-11-12):
  SSID, signal quality, negotiated rate and PHY generation all come through
  `wlanapi`. This closes the last "written but never run on hardware" path in the
  probe layer.
- Still outstanding on that path: dBm is an estimate (`quality / 2 - 100`) rather
  than a scanned RSSI, neighbour/channel advice is not implemented, and the macOS
  (CoreWLAN) and Linux (`/proc/net/wireless`) radio branches do not exist — those
  platforms report "wireless" with no numbers.

### Tests

- 101 unit tests (was 70 at 0.2.3).

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
