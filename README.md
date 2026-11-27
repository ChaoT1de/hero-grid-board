# 评价表拼图板（Hero Grid Board）

![banner](docs/banner.png)

> 按一下 `Ctrl+Alt+A` 框一块屏，图片直接落进对应格子的排行榜 / 评价表小工具。
> Windows 独立窗口（WebView2 内核），不联网，Excel 式多表，整列渐变配色，一键导出 PNG。

[![build](https://github.com/ChaoT1de/hero-grid-board/actions/workflows/build.yml/badge.svg)](https://github.com/ChaoT1de/hero-grid-board/actions/workflows/build.yml)
![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)
![license](https://img.shields.io/badge/license-MIT-green)

「按一下热键框一块屏 → 直接落进对应格子 → 导出整张表」。现在是**自己的窗口**，不再套浏览器：用 Windows 自带的 **WebView2** 内核渲染页面，双击就是一个普通小程序（有图标、有标题栏、有托盘）。

> 前提：系统里有 WebView2 运行时（Win10/11 装过 Edge 的机器基本都有；本机已确认 `157.0.4341.60` 在位）。没有的话会自动退回“Chrome/Edge 应用窗口”模式，并在 `startup.log` 里写明原因。

## 文件

```
拼图板.exe                      主程序（48 KB，原生窗口 + 托盘 + 全局热键 + 截图）
Microsoft.Web.WebView2.Core.dll  WebView2 的 .NET 封装（随 exe 一起放）
WebView2Loader.dll               WebView2 加载器（随 exe 一起放）
app.ico                          窗口/任务栏图标
index.html                      界面与全部业务逻辑（单文件，改需求只改它）
launcher.cs                      主程序源码
build-exe.bat                    一键重编（系统自带 csc.exe）
诊断.bat                          打不开时先跑它
启动.bat                          纯浏览器兜底入口（不依赖 WebView2）
manifest.webmanifest / sw.js / icon-*.png   PWA 资源（暂缓，暂未使用）
双端方案.md                       手机端方案（暂存）
```

> 旧的 `评价表拼图板.exe` 是上一版（Chrome 应用窗口）。它还在托盘里跑着的话会锁住文件，先在托盘图标右键 **退出**，再把它删掉即可，之后只用 `拼图板.exe`。

## 怎么用

1. 双击 `拼图板.exe` → 出现自己的窗口，右下角托盘有个图标。
2. 在任何软件里按 `Ctrl+Alt+A` → 全屏变暗框选 → 松手，图**直接出现在画板当前选中的格子里**，选中框自动右移一格，可以连续按。
3. 也可以：窗口里点 `📷 截图`（浏览器抓屏）、`🖼 相册`、`Ctrl+V`、拖图片文件进去。
4. 拖 `⠿` 换格子、右键菜单、Sheet 多表、自动对齐/吸附、撤销、导出 PNG —— 和之前一致。

## 启动参数

| 命令 | 作用 |
| --- | --- |
| `拼图板.exe` | 独立窗口 + 托盘 + `Ctrl+Alt+A` |
| `拼图板.exe --browser` | 强制用 Chrome/Edge 应用窗口（旧的稳妥路径） |
| `拼图板.exe --no-tray` | 不常驻托盘（也就没有全局热键） |
| `拼图板.exe --serve` | 额外开局域网只读服务，给手机/平板看 |
| `拼图板.exe --diag` | 分阶段写 `startup.log` 后自动退出 |
| `拼图板.exe --test-capture a.png` / `--selftest` | 抓屏 / 热键链路自检 |

## 数据放在哪 / 会不会“表没了”

- 新的独立窗口用自己的配置目录：**程序目录下的 `.webview`**（退而求其次 `%TEMP%`、`%LOCALAPPDATA%`）。
- 旧版（Chrome 应用窗口）的数据在 Chrome 的 profile 里，两者**不互通**。第一次打开如果看到空白表，而旧窗口还开着：
  旧窗口点 `💾 保存` → 新窗口点 `📂 读取`，搬一次即可。
- 之后所有数据都在 `.webview` 里，跟着程序目录走（便携）；换机器复制整个文件夹就行。

## 点一下就崩 → 真凶是第三方注入（已修）

事件日志（`事件查看器 → Windows 日志 → 应用程序`）里的两条记录说得很清楚：

```
Faulting application name: 拼图板-new.exe
Faulting module name: GPP64.dll          ← C:\Users\...\GamePPSDK\1.2.1615.625\GPP64.dll
Exception code: 0xc0000005

Faulting application name: 拼图板-new.exe
Faulting module name: clr.dll
Exception code: 0xc0000005
```

`GPP64.dll` 是**游戏加加（GamePP）**的游戏内覆盖层，它把自己注入到所有 GUI 进程里画浮层。它的访问违例把我们的进程打穿，紧接着 CLR 也跟着崩——所以是“过一会儿才崩、进程已经没了、弹窗来不及截图”（那是 Windows 错误报告在事后弹的）。

修法：进程一启动就设置两个内核缓解策略（不需要管理员权限）：

```csharp
SetProcessMitigationPolicy(ProcessSignaturePolicy,  { MicrosoftSignedOnly = 1 });   // 只允许微软签名的 DLL 进驻
SetProcessMitigationPolicy(ProcessExtensionPointDisablePolicy, { DisableExtensionPoints = 1 });
```

验证（同一台机器、同一份代码，加固前后对比）：

| | 加固前 | 加固后 |
| --- | --- | --- |
| 进程退出 | `0xC0000005`（无日志、无 `FORM:`/`EXIT:`） | `exit=0`，`FORM: 关闭` → `K` → `EXIT` |
| 注入模块 | `GAMEPP-IPC-OVERLAY` 相关报错、`GPP64.dll` 在模块列表里 | `H3: 没有发现外部注入模块` |
| 策略 | — | `H1: 仅允许微软签名 DLL = True` / `H2: 禁用扩展点注入 = True` |

副作用：这类进程里，其它**非微软签名**的注入 DLL（部分外挂、部分游戏助手、某些杀软的界面钩子）也会被挡在门外——对这个小工具来说正是想要的结果。

### 会被系统弹一个「损坏的映像 0xc0000428」

这是**加固生效时的正常提示**，不是程序坏了：

```
评价表拼图板: 拼图板.exe - 损坏的映像
...\GamePPSDK\...\GPP64.dll 没有被指定在 Windows 上运行 ... 错误状态 0xc0000428
```

`0xc0000428` = `STATUS_INVALID_IMAGE_HASH`，意思是“这个 DLL 被策略拒绝加载”。Windows 用“损坏的映像”这种吓人的措辞，其实是在拦游戏加加的覆盖层。**点确定即可，画板在弹窗后面正常运行。**

程序启动时已经调用 `SetErrorMode(SEM_FAILCRITICALERRORS)` 试图压掉这类加载器弹窗；如果你的机器上还会弹，可以按下面的档位取舍：

| 启动参数 | 效果 |
| --- | --- |
| （默认） | H1+H2 全开：最能防崩，可能弹一次“损坏的映像” |
| `--harden=ext` | 只禁扩展点（AppInit 等），不弹窗；但覆盖层可能仍能注入 |
| `--harden=none` | 完全不加固，回到被注入 DLL 带崩的风险 |

最干净的做法：在**游戏加加**里关掉「游戏内覆盖 / Overlay」，然后一直用 `--harden=none`——两边都不打架。

## 打不开 / 窗口没出现

1. 先双击 **`诊断.bat`**：会用 `--diag` 跑一遍并把 `startup.log` 丢进记事本。日志按阶段记：DPI → index.html → 单实例 → 热键 → 托盘 → WebView2 环境 → 控制器 → 导航 → 关闭原因，断在哪一步一眼看到。
2. 日志里若是 `内嵌内核失败 … 0x8000FFFF (E_UNEXPECTED)`，说明这台机器上 WebView2 起不来，直接用 `拼图板.exe --browser`。
3. 程序有自愈：WebView2 启动失败会在程序目录留下 `.webview-pending` 标记，下次启动**自动**改走浏览器模式；删掉这个标记可重新尝试独立窗口。

### 卡死/崩溃时要看的三行

现在日志会记录这些（用来区分“宿主崩”还是“页面崩”）：

| 日志行 | 含义 |
| --- | --- |
| `JS: first-click` | 点在页面上的第一次操作已经到了页面 |
| `JS: js-error: …` / `JS: js-reject: …` | 页面里的 JS 抛错了（带文件名和行号） |
| `W8: 渲染进程失败 kind=…` | WebView2 的渲染进程挂了（内核层面的问题，不是页面逻辑） |
| `FORM: 关闭 reason=…` / `EXIT: 进程结束` | 正常退出 |
| 只有 `W…` 就没有下文 | 宿主进程被直接带走（多半是原生崩溃，需要抓 dump） |

判断方法：如果日志里**有** `JS: first-click` 然后没了 → 问题在页面/渲染器；如果**连** `first-click` 都没有 → 宿主（C# 那层）在把点击交给内核之前就崩了。

### 几个容易误会的地方

- **`--diag` 的窗口会 3 秒后自己关**（诊断用），标题会显示 `[诊断模式 3 秒后自动关闭]`。别把它当成闪退。
- **托盘图标**：Windows 11 默认把新图标塞进任务栏的 **`^` 折叠区**。点 `^` 找到蓝灰色的图标，拖到任务栏上就是常驻显示。启动时会弹气泡提示当前热键。
- **关窗口＝退出程序**（托盘图标也会消失），这样不会留后台僵尸；想让它继续待命就**最小化**，热键照样能用。
- **热键会自动降级**：`Ctrl+Alt+A` 被占用时依次试 `Ctrl+Alt+B`、`Ctrl+Alt+小键盘0`，实际生效的键会写在日志和托盘菜单里。
- 日志末尾的 `FORM: 关闭 reason=…` / `EXIT: 进程结束` 是正常退出；如果只有 `W…` 之后就没了，那才是真崩溃。
- **不要用 `explorer.exe` 兜底打开页面**：会弹 `explorer.exe - 应用程序无法正常启动 (0xc0000142)`，看着像程序坏了。现在内置内核起不来时只在窗口里显示说明 + 一个“用浏览器打开”按钮。

### 窗口 / 缩放 / 性能

- 窗口可以自由拖动缩放（最小 720×460）。内核的位置和大小在 `OnResize` / `OnMove` 时都会同步，并调用 `NotifyParentWindowPositionChanged()` —— 少了这一步内容会跟不上窗口，看着像“不能改大小”。
- 工具栏 **「随窗口」**（默认开）：窗口变窄时自动缩小表格，**只缩不放大**，下限 35%，避免小窗时字看不清；不想要就关掉，缩放完全按滑杆来。
- 性能：放图/换格/清空现在只重画那一个格子（不再整表重建）；自动保存从 350ms 防抖放宽到 900ms（整本是带 base64 图片的 JSON）。

## 已知边界

- 开发环境的沙箱阻止了 WebView2 的浏览器子进程，所以**内核渲染这条路我在这里没能跑通验证**；已验证的是：环境创建成功（能读到运行时版本 157）、失败检测、自动退回浏览器、崩溃标记自愈、抓屏自检、热键注册。真机上若还有问题，`startup.log` 会说明。
- 内嵌窗口里 `getDisplayMedia` 可能不可用（WebView2 不弹屏幕选择器）。所以窗口内的 `📷 截图` 可能降级；**主力方式就是 `Ctrl+Alt+A` 全局框选**，它走 GDI 抓屏，不依赖浏览器权限。
- 全局热键的图通过 WebView2 的消息通道直推页面（不再靠模拟 `Ctrl+V`），同时仍往剪贴板放一份备用。

## 维护约定（踩过的坑）

- **`.ps1` 必须带 UTF-8 BOM**。PowerShell 5.1 对无 BOM 文件按 ANSI(GBK) 解码，中文会把引号吃掉，报 `Missing closing ')'` 假语法错；反过来，**生成 GBK 文件的那个 `.ps1` 自己也得带 BOM**，否则输出文件名会变成 `璇婃柇.bat`。
- `.bat` 里有中文时：文件存 **GBK + CRLF**，开头 `chcp 936 >nul`。
- 改需求只改 `index.html`；只有改 `launcher.cs` 才需要跑 `build-exe.bat`。

## 数据结构

```json
{
  "v": 2, "active": 0,
  "view": { "cellSize": 96, "gap": 6, "labelW": 250, "zoom": 1, "autoAlign": true, "showGuides": true },
  "sheets": [
    { "name": "Sheet1", "title": "对局评价表", "cols": 6,
      "rows": [ { "label": "夯", "color": "#f28b82",
                  "cells": [ { "img": "data:image/webp;...", "ox": 0, "oy": 0, "sc": 1 } ] } ] }
  ]
}
```

老的 v1（单表）存档会自动包成 Sheet1。

---

## 开源到 GitHub

### 仓库该带什么

```
index.html                  源码主体（页面 + 全部业务逻辑）
launcher.cs                 C++ 宿主（WinForms + WebView2）
app.ico                     图标
build-exe.bat               本地一键构建
get-webview2.ps1            从 NuGet 官方源拉 WebView2 SDK（仓库里不放 DLL）
诊断.bat / 启动.bat          辅助入口
LICENSE                     MIT
SECURITY.md                 威胁模型与报告方式
THIRD-PARTY-NOTICES.md      第三方组件与再分发说明
.gitattributes  .gitignore
.github/workflows/build.yml CI：构建 + 打 zip + tag 时发 Release
```

**不要提交**（`.gitignore` 已挡）：`*.exe`、`startup.log`（含本机路径）、`.webview/`（浏览器配置与数据）、`Microsoft.Web.WebView2.Core.dll`、`WebView2Loader.dll`。

### 初始化并推送

```bash
cd hero-grid-board
git init -b main
git add .
git commit -m "feat: 评价表拼图板 v1（独立窗口 / 全局框选 / 多表 / 渐变配色）"

# 建远程并推送（需要已安装 GitHub CLI 且 gh auth login）
gh repo create 评价表拼图板 --public --source=. --remote=origin --push
# 或不装 gh：先在网页建空仓库，然后
# git remote add origin https://github.com/<you>/<repo>.git && git push -u origin main
```

### 发版

```bash
git tag v1.0.0 && git push origin v1.0.0
```

CI 会自动：拉 WebView2 SDK → `csc` 编译 → 打包 zip（含 `SHA256SUMS.txt`）→ 挂到 Release。Windows 版必须在 `windows-latest` 上跑，因为要 `.NET Framework` 的 `csc.exe` 与 `netstandard` facade。

### 安全相关（本仓库已经处理）

| 项目 | 处理方式 |
| --- | --- |
| 目录穿越 / 任意文件读 | `--serve` 的路径做了规范化 + `root\` 前缀校验 + 拒绝 `..` + 扩展名白名单（只放行页面资源，`startup.log`、存档拿不到） |
| 慢速/超长请求打爆内存 | 请求行与 header 逐行限量读取（2 KB/行、≤60 行），并发连接上限 8，接收超时 5s |
| 恶意存档 | 导入的 `img` 只认 `data:image/...;base64`，行数 ≤200、每行格 ≤50 |
| XSS | 所有用户文本走 `textContent`，没有 `innerHTML` 拼接 |
| 空指针/越权消息 | WebView2 只接受 `data:image/...` 消息；`NewWindowRequested` 一律拦截；不开 DevTools |
| 第三方 DLL 注入（游戏加加覆盖层） | `MicrosoftSignedOnly` + 禁用扩展点，可用 `--harden=none` 关 |
| 隐私 | 无任何网络请求；截图只在内存中流转。README 里明写，别人才敢用 |
| 依赖 | 零 npm / 零运行时依赖，只有两枚 WebView2 官方 DLL，CI 从 NuGet 拉 |

### 还需要你自己决定的几件事

1. **代码签名**：没有证书的话，Release 里的 exe 会被 SmartScreen 标成「未知发布者」。要么买 OV/EV 证书，要么在 README 里主推「自行构建」（`build-exe.bat` 一条命令）。
2. **把 `<YOUR NAME>` 换成你的名字/ID**（`LICENSE` 里）。
3. **Issue 模板 / Discussions**：建议加一个 `bug_report.yml`，让反馈带上 `startup.log` 的最后 20 行（这个项目的坑九成在日志里）。
4. **Actions 权限**：workflow 已经写成 `permissions: contents: read`，发布那步由 `softprops/action-gh-release` 自己提升，不需要 PAT。
5. **版本号**：现在 exe 的 AssemblyVersion 是 0.0.0.0，建议在 `build-exe.bat` 里加 `/win32manifest` 或改用 `/define` 打版本，方便用户在「文件属性」里核对。
