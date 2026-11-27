# 评价表拼图板

![banner](docs/banner.png)

按一下 `Ctrl+Alt+A` 框一块屏，图片**直接落进对应格子**的排行榜 / 评价表工具。
Windows 桌面程序，一个窗口一个 exe，**不联网**，排完一键导出 PNG。

[![build](https://github.com/ChaoT1de/hero-grid-board/actions/workflows/build.yml/badge.svg)](https://github.com/ChaoT1de/hero-grid-board/actions/workflows/build.yml)
![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)
![license](https://img.shields.io/badge/license-MIT-green)

<!-- 演示：录好后取消注释，把 docs/demo.gif 一起提交
![demo](docs/demo.gif)
-->

---

## 它解决什么

做「从夯到拉」这类排行榜的人，素材通常来自**视频/游戏画面里的截图**。现成工具（TierMaker、哈基榜、各种 tier list 生成器）都要求你先把图片存成文件再上传，左边档位也只能写两三个字。

这个工具补的就是这条缝：

| | 现成的 tier list 工具 | 这个 |
| --- | --- | --- |
| 取图 | 先截图存盘 → 上传 | **按热键框选，直接落进格子** |
| 左栏 | 短档位名（S/A/B…） | 一整句锐评，可换行、可调字号、可加粗 |
| 行列 | 模板固定 | 行、列、每行格数都能随时加减 |
| 对齐 | 整行左对齐堆图 | 严格网格 + 可关的自动对齐吸附 |
| 配色 | 固定几套 | 逐行纯色 / 预设渐变 / 自定义 / **整列渐变**（首行色→末行色自动插值） |
| 多表 | 一般只有一张 | Excel 式 Sheet1/Sheet2… |

## 功能

- **全局热键框选**：`Ctrl+Alt+A` → 全屏变暗框选 → 松手即落格，并自动跳到右一格，可连续按
- **其它取图方式**：窗口内截图、`Ctrl+V`、拖图片文件（多选连填）、从相册/文件夹导入
- **网格**：加减行、每行独立加减格、左栏文字直接编辑、行底色右键可换
- **配色**：10 色盘 / 8 组预设渐变 / 自定义两色＋方向 / 整列渐变
- **多表**：Sheet 标签栏，双击改名、右键复制删除、`Ctrl+PgUp/PgDn` 切换
- **导出**：整表 PNG（2 倍分辨率，含渐变）、JSON 存档（可手改、可批量生成）
- **自动保存**：关掉再开还在；换机器把整个文件夹拷走即可（便携）

## 快速开始

1. 到 [Releases](https://github.com/ChaoT1de/hero-grid-board/releases) 下载 zip，解压到任意目录；
2. 双击 **`拼图板.exe`**（右下角托盘会出现图标）；
3. 任何软件里按 `Ctrl+Alt+A`，框一块屏，松手。

**系统要求**：Windows 10/11 x64，装了 WebView2 运行时（Win10/11 装过 Edge 的机器基本都有）。
没有的话会自动退回「Chrome/Edge 应用窗口」模式，功能一样。

### 从源码构建

```powershell
powershell -ExecutionPolicy Bypass -File get-webview2.ps1   # 拉 WebView2 SDK（仓库不放二进制）
build-exe.bat                                               # 用系统自带 csc.exe 编译
```

---

## 用法

**三步**：先把左栏的档位文字改好 → 选中第一格 → 连续按 `Ctrl+Alt+A` 截图，图会依次填进去。

### 键鼠

| 操作 | 方式 |
| --- | --- |
| 全局框选 | `Ctrl+Alt+A` |
| 粘贴剪贴板图片到选中格 | `Ctrl+V` |
| 拖动换格子 | 拖图，或按住格子左上角 `⠿`（`Ctrl` 拖＝复制） |
| 微调图片位置 | 关掉「自动对齐」后拖动，方向键 1px，滚轮缩放，松手吸附 |
| 撤销 / 清空格 / 取消 | `Ctrl+Z` / `Delete` / `Esc` |
| 切换表 | `Ctrl+PgUp` / `Ctrl+PgDn` |
| 加行 | 表格底部 `＋ 新一行`，或工具栏同名按钮 |
| 加/减一格 | 行悬停右上角 `+` / `−` |

### 命令行参数

| 参数 | 作用 |
| --- | --- |
| （无） | 独立窗口 + 托盘 + `Ctrl+Alt+A` |
| `--browser` | 强制用 Chrome/Edge 应用窗口（内核出问题时的对照） |
| `--no-tray` | 不常驻托盘（也就没有全局热键） |
| `--serve` | 额外开局域网只读服务，手机/平板同 Wi-Fi 可看 |
| `--diag` | 分阶段写 `startup.log` 后自动退出（排障用） |

---

## 注意事项

1. **数据在哪**：程序目录下的 `.webview` 文件夹（便携）。整个文件夹拷走就搬家；本地 `JSON 存档` 是另一条备份路。
2. **关窗口＝退出**（托盘图标一起消失）；想让它待命就**最小化**，此时热键仍然可用。
3. **托盘图标可能被折叠**：Windows 11 默认把新图标塞进任务栏的 `^` 区域，点开拖出来即可常驻。
4. **会弹「损坏的映像 0xc0000428」？** 那是程序在拒收第三方注入 DLL（游戏加加 / 加速器的游戏内覆盖层，实测它会把本程序带崩）。点「确定」即可，不影响使用；嫌烦可以用 `--harden=ext` 启动，或在游戏加加里关掉「游戏内覆盖」。
5. **未签名的 exe**：首次运行可能被 SmartScreen 提示「未知发布者」。可以核对 Release 里的 `SHA256SUMS.txt`，或者直接按上面的步骤自行构建。
6. **隐私**：全程不联网。截图只在本机内存里流转，不落盘、不上传。唯一的网络行为是你手动加 `--serve` 后，本机开一个只读静态服务给同一局域网的设备看。
7. **图片会被压缩**：入库时统一缩到长边 512px 的 WebP，保证上百张图也不卡；需要更高清可以改 `index.html` 里的 `normalizeImage(src, max)`。
8. **手机端暂不支持**：规则和取舍写在 [docs/手机端方案.md](docs/手机端方案.md)，PWA 那套文件还留在仓库里。

## 出问题了先看这里

双击 **`诊断.bat`**：它用 `--diag` 跑一遍，并把 `startup.log` 摊在记事本里。

| 日志里看到 | 意思 |
| --- | --- |
| `W0: apartment=MTA` | 主线程套间错了（`[STAThread]` 被弄丢），内核必失败 |
| `W3b … E_UNEXPECTED` | WebView2 起不来（多半是另一个实例占着 `.webview`） |
| `W8: 渲染进程失败` | 内核崩了，不是页面逻辑问题 |
| 有 `W…` 却没有 `FORM:`/`EXIT:` | 进程被原生崩溃带走，去事件查看器看 Application Error |
| 有 `JS: first-click` 之后没了 | 问题在页面/渲染器，不在宿主 |

更多排查写在 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 参与

- 想改代码：看 [CONTRIBUTING.md](CONTRIBUTING.md)（代码地图、`[STAThread]` 和 `--serve` 校验这些别踩的地方）
- 报 bug：用 issue 模板，附 `startup.log` 最后 20 行
- 安全相关：不要开公开 issue，按 [SECURITY.md](SECURITY.md) 私下联系

## 许可

[MIT](LICENSE)。第三方组件（WebView2 等）见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
版本变化见 [CHANGELOG.md](CHANGELOG.md)。
