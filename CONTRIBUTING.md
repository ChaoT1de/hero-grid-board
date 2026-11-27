# 参与开发

这个项目刻意保持“小”：**一份 `index.html` 装下所有界面逻辑，一个 `launcher.cs` 装下窗口/托盘/热键/内嵌内核**。没有 npm、没有框架、没有构建链——改完存盘、重开窗口就是新版。

## 环境

- Windows 10/11 x64
- .NET Framework 4.8（系统自带）+ 系统自带的 `csc.exe`
- Edge WebView2 运行时（Win10/11 基本都装了；运行 `拼图板.exe --diag` 看 `W2: env ok` 就知道）

## 跑起来

```bat
:: 1) 取两个 WebView2 DLL（仓库里不提交二进制）
powershell -ExecutionPolicy Bypass -File get-webview2.ps1

:: 2) 编译
build-exe.bat

:: 3) 跑
拼图板.exe --diag            :: 只写日志，3 秒自关，适合看启动链路
拼图板.exe                   :: 正常开
拼图板.exe --browser          :: 强制用 Chrome/Edge 应用窗口（内核有问题时的对照）
```

改页面不用重编：窗口里按 **F5** 就重新加载 `index.html`。

## 代码地图

| 位置 | 内容 |
| --- | --- |
| `index.html` → `<style>` | 画板、行标签、选择框、弹窗 |
| `index.html` → `render()` / `buildRow()` / `buildCell()` | 整表重建；`refreshCell()` 是单格增量更新（放图/换格只走这条，别退化成整表 render） |
| `rowLabelBg()` | 左栏取色：逐行配色 or 整列渐变插值 |
| `exportPNG()` | canvas 重绘导出，渐变角度要跟 CSS 对齐（0° 向上、90° 向右） |
| `launcher.cs` → `Main` | **`[STAThread]` 必须在 `Main` 上**，否则 WebView2 直接 `RPC_E_CHANGED_MODE` |
| `launcher.cs` → `Harden()` | 拒收第三方注入 DLL；`--harden=ext/none` 可降级 |
| `launcher.cs` → `Handle()` | `--serve` 的静态服务：白名单 + 前缀校验 + 限量读取，改这里请补测试 |

## 提交约定

- 一个 PR 只做一件事；顺手改的格式问题单独提。
- 改了 `launcher.cs` 请在 PR 里说明，重点确认：`[STAThread]`、`DllImport` 签名、`--serve` 的路径校验没被绕过。
- 新功能请同步更新 `README.md` 的「命令行参数」或「注意事项」。
- 日志一律走 `Log()`，不要 `Console.WriteLine`（winexe 没有控制台）。
- **编码**：`.ps1` 必须带 UTF-8 BOM（PowerShell 5.1 会按 ANSI 解码中文，报假语法错）；含中文的 `.bat` 存成 GBK + CRLF 并在开头 `chcp 936`。`.gitattributes` 已经把这两类文件排除在换行转换之外。

## 提 issue 前

先跑 `诊断.bat`，把 `startup.log` 最后 20 行带上（里面含本机路径，自己先遮一下）。九成问题在日志里能直接看出来：

| 日志 | 含义 |
| --- | --- |
| `W0: apartment=MTA` | 主线程套间错了（`[STAThread]` 丢了） |
| `W3b … E_UNEXPECTED` | WebView2 起不来（多半是数据目录被占/运行时缺失） |
| `W8: 渲染进程失败` | 内核崩了，不是页面逻辑 |
| 只有 `W…` 就没有 `FORM:`/`EXIT:` | 进程被原生崩溃带走（查事件查看器的 Application Error） |

## 许可

提交即表示同意以 [MIT](LICENSE) 发布你的贡献。
