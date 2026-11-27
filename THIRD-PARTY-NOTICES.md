# 第三方组件与许可

本仓库源码以 MIT 发布，但运行/构建时用到的第三方组件各自遵循其原许可：

| 组件 | 用途 | 许可 / 说明 |
| --- | --- | --- |
| **Microsoft Edge WebView2**（运行时 + `Microsoft.Web.WebView2.Core.dll` + `WebView2Loader.dll`） | 窗口内嵌渲染 | 由 Microsoft 提供，随 Windows 10/11 分发；SDK 的 DLL 按 Microsoft 的再分发条款允许随应用一起分发。仓库**不直接提交**这些 DLL，由 `get-webview2.ps1` 从 NuGet 官方源（`Microsoft.Web.WebView2`）拉取 |
| **.NET Framework 4.8**（`csc.exe`、`System.Windows.Forms`、`System.Drawing`） | 编译与运行 | Windows 自带 |
| **Microsoft YaHei / 微软雅黑** | 界面字体 | 随 Windows 提供，仅引用字体名，不打包字体文件 |

## 没有用到的

- 没有 Electron / Chromium 打包、没有 Node 运行时依赖；
- 没有前端框架、没有 npm 依赖、没有 CDN 脚本（`index.html` 完全自包含）；
- 没有第三方统计 SDK。

## 如果你要再分发

- 发布 zip 里若包含 `Microsoft.Web.WebView2.Core.dll` 与 `WebView2Loader.dll`，请保留其原始文件不做修改，并在发布说明里注明来源与版本；
- 不要把 `app.ico` 之外的图标/素材当成自己的商标；
- MIT 许可要求保留 `LICENSE` 中的版权与许可声明。
