# AirNet 隐私政策 / Privacy Policy

最后更新：2026-11-13 · Last updated: 13 November 2026

**AirNet 不收集、不上传、不出售任何个人数据。** 应用没有账号、没有分析 SDK、没有广告、没有遥测。

**AirNet does not collect, upload, or sell personal data.** There is no account, no
analytics SDK, no advertising, and no telemetry.

---

## 一、应用会做什么网络请求

测量本身就是联网行为，所以这一节逐条列清。**所有请求都由你点击「开始检测」或对应按钮触发**，没有后台自动外发。

| 功能 | 目标 | 发出什么 |
| --- | --- | --- |
| 延迟 / 丢包 | `223.5.5.5`（ICMP） | 标准 ICMP echo，无载荷内容 |
| 抖动 / 握手 | `223.5.5.5:443`（TCP） | TCP 握手后立即关闭，不发送应用数据 |
| 首包分解 | `www.baidu.com:443` | 一次 HTTPS GET `/`，只读响应首行，不下载正文 |
| 网站延迟 | 11 个默认站点（见下） | 每站 3 次 HTTPS GET `/`，只读首字节 |
| 出口 IP | `www.cloudflare.com/cdn-cgi/trace`、`ip.3322.net` | 普通 HTTPS GET，用于显示公网地址 |
| 带宽测速 | `speed.cloudflare.com/__down`、`/__up` | 下行读 4 秒；上行发送 4 MB 随机字节 |
| DNS 对比 | `223.5.5.5:53`、`1.1.1.1:53`（UDP） | 对同一域名的 A 记录查询 |
| 链路路径 | 系统 `tracert` 到 `223.5.5.5` | TTL 递增的探测包 |
| MTU | `223.5.5.5` | 带 DF 标志的 ICMP，二分探测 |
| 游戏模式 | `stun.l.google.com:19302`（UDP） | 标准 STUN 绑定请求 |
| 邻区扫描 | **仅本机** | 通过 WLAN API 读取周边 AP，不发包 |

默认站点：`www.baidu.com`、`www.qq.com`、`www.douyin.com`、`www.xiaohongshu.com`、
`www.bilibili.com`、`www.zhihu.com`、`www.weibo.com`、`github.com`、`www.google.com`、
`www.youtube.com`、`x.com`。自定义站点由你自己添加，只会访问你添加的域名。

这些请求不携带账号、设备标识或本机文件内容；对方服务器按常规会看到你的公网 IP 与请求时间——这是任何联网请求都无法避免的部分，AirNet 不做额外标识。

## 二、应用在本地保存什么

全部保存在本机，**从不上传**：

| 文件 | 内容 |
| --- | --- |
| `history.jsonl` | 每次测量的评分、延迟、抖动、丢包、上下行带宽、网卡类型与 SSID |
| `settings.json` | 主题、自动刷新、是否保留历史等偏好 |
| `sites.json` / `sites.jsonl` | 你添加的自定义站点与其测量历史 |
| `dns-backup.json` | 改动 DNS 前的原始值（用于还原） |
| `crash.log` | 崩溃时的异常与版本信息，仅本机 |

位置：Windows 为 `%LOCALAPPDATA%\AirNet`；macOS 为
`~/Library/Application Support/AirNet`；Linux 为 `~/.config/AirNet`。
从 Microsoft Store 安装时，系统会把该目录重定向到应用包的数据目录。

历史默认保留 30 天，超出即自动裁剪。设置页「清空历史」可立即删除全部记录；
删掉上述目录等同于完全清除。

## 三、应用会修改什么系统设置

只有一处，且必须由你确认：

- **DNS 优化**：应用先展示完整命令，你点击「应用（需管理员）」并确认 UAC 后才会执行，
  同时把原值写入 `dns-backup.json` 以便「还原」。
- **Microsoft Store 版不执行提权**：包内进程无法弹 UAC，应用只显示命令供你复制，
  不会自己修改任何系统设置。

除此之外不修改路由、不安装驱动、不常驻后台、不开机自启。

## 四、诊断包

设置页「导出诊断包」会把上面列出的本地文件打包成 zip，**由你选择保存位置**。
它不会被自动上传；是否发给别人由你决定。包内不含网页内容、不含账号信息，
但可能包含局域网地址——分享前请自行确认。

## 五、儿童

应用不收集任何人的数据，包括儿童。

## 六、变更与联系方式

本政策的修改会提交到
<https://github.com/RyzenWang/AirNet/blob/main/docs/PRIVACY.md>，
并更新顶部的日期。问题请开 issue：
<https://github.com/RyzenWang/AirNet/issues>。

---

## English summary

AirNet sends network requests because measuring the network requires them. Every
request is triggered by a user action — there is no background traffic, no
analytics, and no account.

- **Measurements** go to `223.5.5.5` (ICMP/TCP/traceroute/MTU), `www.baidu.com`
  (first-byte breakdown), `speed.cloudflare.com` (bandwidth), `www.cloudflare.com`
  and `ip.3322.net` (egress IP), `223.5.5.5`/`1.1.1.1` over UDP 53 (DNS
  comparison), `stun.l.google.com` (game mode), and the site catalog over HTTPS.
  They carry no identifier beyond what any HTTPS request includes.
- **Local data** (history, settings, custom sites, DNS backup, crash log) stays on
  the device, is kept for 30 days, and is never uploaded.
- **The only system change** is the DNS switch, and only after you confirm it. The
  Microsoft Store build cannot elevate, so it only shows the command to copy.
- **The diagnostic bundle** is written where you choose; it is never sent anywhere
  automatically.

Contact: <https://github.com/RyzenWang/AirNet/issues>
