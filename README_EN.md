# AirNet

Local-first network diagnostics for Windows, macOS and Linux. One scan answers: how good is
this network, how much bandwidth does it have, and is the problem the LAN, the ISP or the far
end? No driver, no resident agent, no account, no telemetry.

[中文](README.md) · [Microsoft Store](https://apps.microsoft.com/detail/9NH3RQW622BK) ·
[Releases](https://github.com/RyzenWang/AirNet/releases/latest) ·
[Docs](https://ryzenwang.github.io/AirNet/)

## What it measures

- ICMP loss and TCP handshake latency, jitter and P95
- DNS / TCP / TLS / TTFB breakdown for the first request
- Wireless SSID, signal, link rate and PHY generation
- IPv4/IPv6 side-by-side timing; proxy/VPN interference
- Download and upload throughput (manual scans, or the separate speed page)
- Site latency for 11 defaults plus custom sites, with HTTP status
- Gateway → ISP → far-end attribution, traceroute, MTU
- Local 30-day history, trends, A/B comparison and a 1080×1350 share card
- Wi-Fi neighbor scan (read-only), game and meeting modes
- Self-contained HTML report and a local diagnostic bundle

## Install

| Channel | Version | Notes |
| --- | --- | --- |
| Microsoft Store | `1.0.0.0` | signed by Microsoft, Store updates |
| GitHub portable | `0.3.1` | Windows zip, macOS zip, Linux tar.gz |

The Store build cannot self-elevate, so DNS changes are copy-the-command; the portable build
keeps the one-click path on Windows, macOS (`osascript`) and Linux (`pkexec`).

## Build

Requires .NET 10 SDK.

```bash
dotnet run --project src/AirNet.App
dotnet test tests/AirNet.Core.Tests/AirNet.Core.Tests.csproj
powershell -File packaging/pack-aot.ps1
```

## Status

`0.3.1` is the current portable/source line. `0.3.0` was the first dual-channel release; the
Store package uses its own `1.0.x.0` line. macOS and Linux arm64 are experimental: CI builds
them, but they have not been run on real hardware and are unsigned.

MIT licensed. Chinese documentation is the reference: [README.md](README.md),
[docs/FEATURES.md](docs/FEATURES.md), [docs/TECH_PLAN.md](docs/TECH_PLAN.md).
