---
layout: default
title: runFullTrust 用途说明
---

# runFullTrust 用途说明

Microsoft Store 对**受限能力（restricted capability）**要求逐项说明用途。
AirNet 的包声明了 `rescap:runFullTrust`，提交时把下面的文本贴进 Partner Center 的
Justification 字段即可。

包内实际声明（`packaging/msix/AppxManifest.xml`）：

```xml
<Application Id="AirNet" Executable="AirNet.exe" EntryPoint="Windows.FullTrustApplication">
...
<Capabilities>
  <rescap:Capability Name="runFullTrust" />
</Capabilities>
```

## 英文版（推荐，约 120 词）

```
AirNet is a network diagnostics tool. It measures latency, jitter, packet loss,
DNS resolution, MTU and throughput by sending ICMP echo requests, timing raw TCP
handshakes, issuing UDP DNS queries to the configured and public resolvers, and
sending UDP STUN requests. Wi-Fi details (SSID, signal quality, PHY generation)
come from the WLAN API, and the route view runs the built-in Windows traceroute.
None of these APIs are available inside an app container, so the package uses a
full-trust Win32 entry point (Windows.FullTrustApplication).

The app runs with the signed-in user's token: no elevation, no service, no driver,
no startup task. Network activity only happens after the user starts a scan. The
Store build never changes system settings - DNS changes are displayed as text for
the user to run. Measurements are stored locally and never uploaded.
```

## 中文版

```
AirNet 是网络诊断工具，通过 ICMP 回显、原始 TCP 握手计时、UDP DNS 查询与 UDP STUN
请求测量延迟、抖动、丢包、解析耗时、MTU 与带宽；Wi-Fi 信息来自 WLAN API，逐跳视图
调用系统自带的 traceroute。这些 API 在应用容器内均不可用，因此必须使用完全信任的
Win32 入口（Windows.FullTrustApplication）。

应用以当前登录用户的令牌运行：不提权、不装服务、不装驱动、不开机自启。仅在用户点击
开始检测后才有网络活动。商店版不修改任何系统设置——DNS 变更只显示为文本供用户自行
执行。测量数据仅保存在本机，从不上传。
```

## 短版（字段字数受限时，约 40 词）

```
Full trust is required for ICMP, raw TCP timing, UDP DNS/STUN queries, WLAN API
and traceroute, none of which are reachable from an app container. The app runs
unelevated as the user, installs no service or driver, and only acts after the
user starts a scan.
```

## 审核常问的五点，文本里已经回答

| 审核会问 | 对应句 |
| --- | --- |
| 为什么不能用普通容器权限 | "None of these APIs are available inside an app container" |
| 是否需要管理员权限 | "no elevation, runs with the signed-in user's token" |
| 是否有后台行为 | "Network activity only happens after the user starts a scan" |
| 是否修改系统设置 | "The Store build never changes system settings" |
| 是否上传用户数据 | "stored locally and never uploaded" |

## 在哪里填

Partner Center → 产品 → **Submission** → **Restricted capabilities** → Justification。
每次提交新包都要重新填一次，内容可以直接复用本节。

隐私政策与数据流向的完整清单见 [隐私政策](PRIVACY.html)。
