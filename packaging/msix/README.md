# MSIX 打包（本地 sideload 与商店提交共用）

这套脚本从 AirPDF 的管线移植而来——那边已经用同一条路把应用发到 Microsoft Store。

## 为什么这么做

| 项 | 说明 |
| --- | --- |
| **不能用单文件发布** | MSIX 要求文件布局，`PublishSingleFile` 打出来是一个 exe + 原生库，无法作为包内容 |
| **AOT 优先** | 体积约为 JIT 的一半，启动远低于 1 秒；默认开启，`-NoAot` 可退回 |
| **自签证书** | 商店提交由微软重新签名，不需要证书；**本地 sideload 必须有受信任的证书** |
| **提权不可用** | 包内进程无法弹 UAC，所以商店版的「一键 DNS」降级为"复制命令"（见 `PackagedApp`） |

## 构建

```powershell
# 自签并打包（本地验证用）
powershell -File packaging/msix/Build-Msix.ps1 -Version 0.2.9.0 -SelfSign

# 商店提交用：不带签名，身份值换成 Partner Center 给的
powershell -File packaging/msix/Build-Msix.ps1 -Version 0.3.0.0 -ForStore `
    -IdentityName <Product identity 里的 Name> `
    -Publisher "CN=<Product identity 里的 Publisher>"
```

产物在 `artifacts/msix/`：

| 文件 | 用途 |
| --- | --- |
| `AirNet-<version>-x64.msix` | 签好名的安装包 |
| `AirNet-TestSigning.cer` | 自签证书公钥（安装时要信任它） |
| `app-x64/` | 文件夹发布产物（可直接跑，用于排查） |
| `layout-x64/` | 打包前的布局，含生成的 MSIX 图标 |

脚本最后会**解包并逐项断言**认证关注点：身份四要素、入口 exe 存在、`runFullTrust`、五个必需图标、8 个 targetsize 变体。任一项失败都以非零退出。

## 本地安装（需要管理员）

Windows 只接受**机器级受信任**证书签名的 sideload 包，这一步必须提权：

```powershell
# 以管理员身份运行 PowerShell
powershell -File packaging/msix/Install-AirNet.ps1
```

它会把证书导入 `LocalMachine\Root` 与 `LocalMachine\TrustedPeople`，然后 `Add-AppxPackage`。
卸载用 `Uninstall-AirNet.ps1`，会把包和证书一起清掉。

> 只导入到 `CurrentUser` 证书库是**不够的**：部署会失败并报 `0x800B0109`（签名的根证书必须是受信任的证书）。

## 装完之后看什么

1. 开始菜单磁贴：脉冲标记应当是**内缩**的，而不是铺满整块；
2. 跑一次检测：评分、双栈、DNS 三方对比都应正常；
3. 设置页的 DNS 卡片应显示「Microsoft Store 版无法自动修改系统 DNS」，并仍给出可复制的命令；
4. 历史在**包数据目录**里，不是 `%LOCALAPPDATA%\AirNet`——MSIX 会重定向，这是正常的。

## 已知差异（商店版 vs 便携版）

| 能力 | 便携版 | 商店版 |
| --- | --- | --- |
| 一键改 DNS（提权） | ✅ | ❌ 只能复制命令 |
| 数据位置 | `%LOCALAPPDATA%\AirNet` | 包私有目录（重定向） |
| 更新 | 手动下载 | 商店负责 |
| 类路径 / CLI 参数 | 可用 | 商店启动不带参数 |
