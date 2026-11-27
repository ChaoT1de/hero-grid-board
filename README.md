# 评价表拼图板

![banner](docs/banner.png)

按 `Ctrl+Alt+A` 框一块屏，图片直接进格子。做「从夯到拉」这类排行榜用的，Windows 桌面程序，不联网。

[![build](https://github.com/ChaoT1de/hero-grid-board/actions/workflows/build.yml/badge.svg)](https://github.com/ChaoT1de/hero-grid-board/actions/workflows/build.yml)
![license](https://img.shields.io/badge/license-MIT-green)
![大肥鱼](docs/dafiyu.png)

<!-- 录好演示后取消注释，把 docs/demo.gif 一起提交
![demo](docs/demo.gif)
-->

## 这是什么

做排行图的素材大多来自视频和游戏里的截图。现成的 tier list 工具都要先把图存成文件再上传，左边的档位也基本只能写两三个字。

这个工具把这段流程缩成三个动作：按热键、框一块屏、图进格子。左栏能写整句话，行和格子随时加减，排完直接导出 PNG。

## 怎么用

到 [Releases](https://github.com/ChaoT1de/hero-grid-board/releases) 下 zip，解压，双击 `拼图板.exe`。右下角托盘会出现图标。

1. 先把左栏的文字改成你要的档位；
2. 点一下第一格的格子；
3. 连续按 `Ctrl+Alt+A`，每截一张就自动落到当前格，然后跳到右边一格；
4. 排完点「导出 PNG」。

图片也可以用别的方式进：窗口里的 `📷 截图`、`Ctrl+V` 粘贴、从资源管理器拖文件进来（多选会依次填）。

### 键鼠

| 做什么 | 怎么做 |
| --- | --- |
| 全局框选 | `Ctrl+Alt+A` |
| 粘贴图片到选中的格子 | `Ctrl+V` |
| 换格子 | 拖图，或按住格子左上角的 `⠿`（按住 `Ctrl` 拖是复制） |
| 微调图片位置 | 关掉「自动对齐」后拖动，方向键挪 1px，滚轮缩放，松手会吸附 |
| 撤销 / 清空当前格 / 取消 | `Ctrl+Z` / `Delete` / `Esc` |
| 切换表 | `Ctrl+PgUp`、`Ctrl+PgDn` |
| 加一行 | 表格底部「＋ 新一行」 |
| 给某行加一格 | 鼠标移到行上，右上角的 `+` |

### 启动参数

| 参数 | 作用 |
| --- | --- |
| 无 | 独立窗口 + 托盘 + 全局热键 |
| `--browser` | 用 Chrome/Edge 的应用窗口打开，内核出问题时的对照 |
| `--no-tray` | 不常驻托盘，也就没有全局热键 |
| `--serve` | 顺便在本机开个只读服务，同一 Wi-Fi 的手机可以打开看 |
| `--diag` | 跑一遍并把过程写进 startup.log，然后自动退出 |

## 和别的工具比

- **取图**：那边是先截图存盘再上传，这边按一次热键就进格子了。
- **左栏**：别家是 S/A/B 这种短标签；这里能写一整句，字号 10–32px 可调，也能加粗。
- **行列**：不用套模板，行数和每行的格子数随时改。
- **配色**：除了逐行选色和渐变，还能设「首行一个色、末行一个色」，中间自动过渡；加行删行会重新算，不用手动调。
- **多表**：底下像 Excel 一样有 Sheet，可以一张放角色、一张放皮肤。

## 注意事项

- Win11 默认把新的托盘图标塞进任务栏的 `^` 里，拖出来固定一下，不然会以为没启动。
- 弹出「损坏的映像 0xc0000428」是在拦游戏加加这类覆盖层 DLL（它会把本程序带崩）。点确定继续用；不想看到就加 `--harden=ext` 启动，或在游戏加加里关掉「游戏内覆盖」。
- 图片入库会缩到长边 512px（WebP）。要更清晰就改 `index.html` 里的 `normalizeImage`。
- 手机端还没做，思路记在 [docs/手机端方案.md](docs/手机端方案.md)。

## 出问题

双击 `诊断.bat`。它会用 `--diag` 跑一遍，然后把 `startup.log` 用记事本摊开。

日志里这几行比较能说明问题：

- `W0: apartment=MTA`：主线程套间不对，内核必然起不来。
- `W3b ... E_UNEXPECTED`：WebView2 没起来，通常是另一个实例还占着 `.webview`。
- 有 `W` 开头的行，但没有 `FORM:` / `EXIT:`：进程是被系统直接带走的，去事件查看器看「应用程序错误」。
- `JS: first-click` 之后没下文：问题在页面里，不在宿主。

## 自己编译

需要 Windows 10/11 x64 和系统自带的 .NET Framework 4.8（`csc.exe`）：

```powershell
powershell -ExecutionPolicy Bypass -File get-webview2.ps1   # 拉 WebView2 的两个 DLL
build-exe.bat                                               # 编译
```

改页面不用重编，窗口里按 F5 就重新加载。

## 其它

- 改代码之前看一眼 [CONTRIBUTING.md](CONTRIBUTING.md)，里面有几个容易踩的坑（`[STAThread]`、`--serve` 的路径校验）。
- 报 bug 请带上 `startup.log` 最后 20 行。
- 安全问题按 [SECURITY.md](SECURITY.md) 私下说，别开公开 issue。
- 许可 [MIT](LICENSE)，第三方组件见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)，版本变化见 [CHANGELOG.md](CHANGELOG.md)。

---

大肥鱼 · 2026
