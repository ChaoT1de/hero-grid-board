# 安全说明

## 这个程序做什么、不做什么

**做**：在本地窗口里显示一个「评价表拼板」，把截图/图片放进格子，导出 PNG / JSON。
**不做**：

- 不发起任何网络请求（没有更新检查、没有遥测、没有云同步、没有埋点）；
- 不上传截图、不读剪贴板历史、不录键、不驻留驱动。

唯一会「开端口」的情形是显式加了 `--serve`：那会在本机开一个**只读**静态文件服务（默认 8788），供同一局域网里的手机/平板打开同一份页面。

## 已知的攻击面（以及本仓库怎么处理）

| 面 | 风险 | 处理 |
| --- | --- | --- |
| `--serve` 静态服务 | 目录穿越、任意文件读取 | 路径规范化后按 `root\` 前缀前缀校验；拒绝含 `..` 的路径；扩展名白名单（只放行页面资源）；`startup.log`、存档 `.json` 之类不在白名单里就拿不到 |
| 同一服务 | 慢速/超长请求打满内存 | 请求行与 header 逐行**限量读取**（2 KB/行、最多 60 行）；并发连接上限 8；接收超时 5 秒 |
| 导入的 `.json` 存档 | 恶意 `img` 字段（外链追踪、奇怪协议）、超大结构卡死界面 | 只接受 `data:image/(png\|jpe?g\|webp\|gif);base64,` 开头的内嵌图；行数上限 200、每行格子上限 50 |
| 截图功能 | 会读到屏幕内容 | 处理后只存在本地；`Ctrl+Alt+A` 抓的图直接进内存并放进画板，不落盘、不出网 |
| WebView2 内核 | 本机页面与宿主之间的消息通道 | 只接受 `data:image/...` 字符串；`AreDevToolsEnabled=false`、`AreDefaultContextMenusEnabled=false`、`NewWindowRequested` 一律拦截 |
| 第三方 DLL 注入 | 游戏加加/加速器的覆盖层会把进程带崩 | 启动即设置 `ProcessSignaturePolicy(MicrosoftSignedOnly)` 与 `ProcessExtensionPointDisablePolicy`，可用 `--harden=none` 关闭 |
| 二进制分发 | 未签名 exe 会触发 SmartScreen | 见下文「签名」 |

## 分发注意

- 仓库**不提交**编译产物和第三方 DLL；`get-webview2.ps1` 从 NuGet 官方源拉取 WebView2 SDK，Release 里的 zip 由 CI 构建。
- 如果要给陌生人用，建议给发布物做代码签名（OV/EV 证书），否则 SmartScreen 会提示「未知发布者」。没有证书也可以，但请在 README 写明「自行构建」的命令。
- 发布页请附上 SHA-256，方便用户校验。

## 报告问题

发现安全问题请**不要**开公开 issue，直接邮件联系维护者（在 GitHub 个人资料里），或使用仓库的「Report a vulnerability」（Security → Advisories）。
描述包括：版本、复现步骤、影响（能读到什么/能执行什么）。48 小时内会回。

## 支持范围

只对最新 Release 与 `main` 分支的最新提交提供修复。
