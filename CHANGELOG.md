# Changelog

All notable changes are documented here. The format follows Keep a Changelog and the
project uses semantic versioning for release tags.

## [1.0.0] - 2026-11-13

First public release, and the first one that can be installed from the Microsoft Store.

### Added

- Microsoft Store packaging. `packaging/msix` builds a signed package for local
  sideload (`-SelfSign`) or an unsigned `.msixupload` for Partner Center (`-ForStore`,
  which refuses to sign because the Store re-signs and a second signature gets the
  submission rejected). The script generates all package images from the 512px PNG
  master, expands the manifest, packs, then unpacks and asserts the twelve things
  certification checks.
- Crash log. `CrashLog.Install()` hooks `AppDomain` and `TaskScheduler` before any UI
  exists and appends version, OS, runtime and the exception to `crash.log` beside the
  history. The writer never throws and rolls over at 256 KB.
- Diagnostic bundle. The settings button that used to do nothing now writes a zip with
  a summary (build, last measurement, privacy note) plus whatever settings, history,
  site history, DNS backup and crash log exist.
- Shared reports carry the last seven days: average score, latency, jitter, loss and
  the worst time-of-day bucket in words, so a report forwarded to an ISP answers "is
  it always like this".
- Radio data on macOS (`system_profiler`) and Linux (`iw` + `/proc/net/wireless`),
  which previously reported only "无线" with no numbers.
- One-click DNS works on macOS (`osascript` with administrator privileges) and Linux
  (`pkexec`), not just Windows.
- `--size WxH` for the screenshot pipeline, and the docs site under `docs/` published
  by `.github/workflows/pages.yml`.

### Changed

- macOS and Linux arm64 are labelled **experimental**: they build in CI and the
  packages install, but nobody has run them on real hardware. The app says so in its
  About card on those platforms only, rather than warning everywhere.
- The Store build cannot elevate, so 一键 DNS degrades to "copy the command" there.
  `PackagedApp` detects package identity through `GetCurrentPackageFullName`; the
  portable build keeps the one-click path.

### Fixed

- The package logos were rainbow noise: `System.Drawing.Icon.ToBitmap()` cannot decode
  PNG-compressed ICO frames, which is what the repository's `.ico` contains.
- The Start tile looked undersized because the 512px master already keeps a 4% margin
  and the generator drew it at 0.82; it now fills 92%, and `.scale-125/150/200/400`
  variants stop Windows from resampling the 100% asset on high-DPI displays.
- Sideloading failed with `0x800B0109` even though the package was correctly signed:
  importing the certificate into `LocalMachine\Root` via `Import-Certificate` pops a
  confirmation that a non-interactive session silently skips. The install script now
  uses `certutil -addstore -f` and verifies both machine stores before installing.

### Verified

- Windows 11 x64, AOT package, sideloaded: install, icons in all three surfaces, scan,
  the Store-build DNS message, and history surviving a restart.
- 182 unit tests.

## [0.2.8] - 2026-11-13

### Added

- History trend page: 7/30-day latency and score curves, local time-of-day buckets and
  recent runs. The evening bucket states its own conclusion ("晚间平均 36.3 ms，高于整体
  25.6 ms") instead of leaving the reader to eyeball the chart.
- Share card: a 1080 × 1350 PNG generated from the current snapshot, with a preview
  window and a file picker; the tray menu exposes the same action.
- The manual "开始检测" now includes a download/upload bandwidth pass after the latency
  probes; the 60-second automatic refresh deliberately skips the upload.

### Fixed

- Normal startup now begins in an explicit pending state instead of showing sample score,
  latency, path or bandwidth values. Starting a scan clears the previous view and fills
  the live/real values; cancel restores the last real view.
- The trend page had no screenshot worth linking: the export pipeline ran against an
  empty data directory, so every capture documented the empty state. `--export` now
  seeds a deterministic week into its throwaway directory, which makes the charts, the
  time-of-day buckets and the recent-runs list visible in the docs without touching the
  user's own history.
  +
  Added six tests around the sample week: record count, ordering, determinism, the
  deliberately bad evenings, score-follows-latency, and the JSONL shape (148 total).

## [0.2.7] - 2026-11-13

### Changed

- Windows, macOS and Linux release builds now use Native AOT through a native-runner
  matrix (`win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`). Assets carry
  an `-aot` suffix. The R2R single-file build remains available as a local fallback.
- Version and footer now report 0.2.7.

### Fixed

- Linux AOT packages are built in an Ubuntu 22.04 container (glibc 2.35), so the
  binary no longer requires `GLIBC_2.38`. Linux releases now ship one `.tar.gz` per
  architecture instead of both zip and tar.gz.
- The startup sample no longer fabricates bandwidth (previously 384.2/92.6 Mbps).
  Until the user runs a speed test, the dashboard, speed page, report and history
  consistently show "未测速" instead of a fixed number.

## [0.2.6] - 2026-11-13

### Added

- Wi-Fi neighbor scan on Windows through `WlanGetNetworkBssList`: SSID, BSSID,
  signal quality, PHY and negotiated rates. Read-only; wired machines skip it
  explicitly instead of showing an empty failure.
- Game mode: six STUN samples measure UDP RTT, jitter and loss and summarize NAT
  mapping behavior. The result is a heuristic, not a full NAT type or a real game
  server RTT.
- Meeting mode: 2MB upload plus TCP-handshake jitter/loss, with a conservative
  1080p / 720p / risk verdict.

### Changed

- Monitor's scenario card is now backed by an on-demand "模式深测" card; both modes
  are separate from the 3-second scan and never run automatically.
- Test suite: 135 tests (0.2.5 was 123).

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
