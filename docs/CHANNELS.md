---
layout: default
title: 双渠道发布
---

# 双渠道发布

AirNet 同时从两个渠道发布：**GitHub Releases（便携包）** 和 **Microsoft Store**。
两者出自同一个 commit、同一份源码，但版本号的规则不同。

## 版本对应

| 渠道 | 版本写法 | 例子 | 谁要求 |
| --- | --- | --- | --- |
| GitHub Release / 便携包 | `X.Y.Z` + tag `vX.Y.Z` | `0.3.0` / `v0.3.0` | 语义化版本 |
| Microsoft Store 包 | `X.Y.Z.0`（四段，末位必须 0） | `0.3.0.0` | Partner Center |

**同号，只是商店多一个 `.0`**。应用内显示、CHANGELOG、tag 全部用三段版本；
四段只出现在 `AppxManifest` 的 `Identity/@Version`。

```powershell
# 便携包（GitHub 渠道）
powershell -File packaging/pack-all.ps1

# 商店包（Microsoft Store 渠道），-Version 补一个 .0
powershell -File packaging/msix/Build-Msix.ps1 -Version 0.3.0.0 -ForStore `
    -IdentityName RyzenWang.AirNet `
    -Publisher "CN=D87D6846-5A2B-4D82-BDB7-8E739EB3F35B"
```

## 为什么商店不用独立版本线

很 tempting 的做法是让商店从 `1.0.0.0` 起步、GitHub 继续 `0.2.x`，但那样会带来两个长期成本：

1. **对不上号**：用户在商店看到 1.0.0、应用里显示 0.2.8，报障时说不清是哪个版本；
2. **两份 CHANGELOG**：要么维护两条历史，要么每次发版手工换算。

商店只要求"四段 + 末位 0 + 每次提交比上次大"，从未要求主版本号从 1 开始。
所以两条线保持同号最省事。

## 这条规则带来的唯一约束

商店每次提交的版本必须**严格递增**。如果只是修复商店包本身（例如清单写错、
资源缺失）而应用版本不变，就需要把补丁号往上抬一位：

```
0.3.0.0  首次提交
0.3.1.0  仅修包，不再动代码
```

这时 GitHub 也应同步打 `v0.3.1`——哪怕只是一个空提交，也要让两条线继续对齐。
**不要**用第四段做递增：商店要求末位为 0。

## 发布顺序

1. 改 `<Version>`（csproj）、`VersionText`（设置页）、页脚三处字面量；
2. CHANGELOG 落版，`docs/release/vX.Y.Z.md` 写中文发布说明（商店正文用它）；
3. 重出截图——页脚带版本号，`--export docs/shots` 与 `--size 1600x900` 各一轮；
4. `git tag vX.Y.Z` 并推送：CI 构建五平台 AOT 包并发布 Release；
5. 打商店包：`Build-Msix.ps1 -Version X.Y.Z.0 -ForStore ...`，上传 `.msixupload`；
6. 两边的产物都来自同一个 commit，`ProductVersion` 里带 commit hash，可反查。

## 渠道差异（用户可见）

| 能力 | 便携版 | 商店版 |
| --- | --- | --- |
| 一键修改 DNS | ✅ 提权执行 | ❌ 只能复制命令 |
| 数据位置 | `%LOCALAPPDATA%\AirNet` | 包私有目录（MSIX 重定向） |
| 更新方式 | 手动下载新包 | 商店推送 |
| 命令行参数 | 可用 | 商店启动不带参数 |

这些差异在应用内也有提示：商店版会在「关于」卡片额外说明提权限制，
macOS 与 Linux arm64 会标注实验性。
