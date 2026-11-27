using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

// 评价表拼图板 · 独立桌面程序（WebView2 内嵌内核，不用开浏览器）
//   默认        ：原生窗口 + 托盘 + Ctrl+Alt+A 全局框选（截图直接送进画板）
//   --serve     ：额外开局域网只读服务
//   --no-tray   ：不常驻托盘
//   --browser   ：退回“Chrome/Edge 应用窗口”模式（WebView2 不可用时也会自动退回）
//   --diag      ：分阶段写 startup.log 后自动退出
//   --shot out.png：渲染页面并截图存盘（自检用，不弹界面）
//   --test-capture out.png / --selftest：抓屏/热键链路自检
static class Launcher
{
    const int HOTKEY_ID = 0xB001;
    const int WM_HOTKEY = 0x0312;
    const int PORT = 8788;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr param);
    [DllImport("user32.dll")] static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr param);

    static NotifyIcon tray;
    static AppForm form;
    static Mutex single;
    static bool capturing = false;
    public static bool serveMode = false;
    const string TITLE = "评价表拼图板";

    /* ================= 入口 ================= */
    [DllImport("kernel32.dll")] static extern uint SetErrorMode(uint uMode);

    [STAThread]
    static void Main(string[] args)
    {
        // 压掉加载器硬错误弹窗（策略拦截 DLL 时会弹“损坏的映像”）
        try { SetErrorMode(0x0001 | 0x0002 | 0x8000); } catch { }
        // 第一件事：拒收第三方注入（游戏加加/加速器的覆盖层 DLL 会把进程带崩）
        // --no-harden 可关闭（万一某个输入法/辅助软件被误伤）
        string hm = FlagValue(args, "--harden");
        if (hm == null && Has(args, "--no-harden")) hm = "none";
        Harden(hm);
        Log("A: 启动, args=" + Join(args));
        LogModules();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Log("THREAD: " + e.Exception); };
        AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Log("UNHANDLED: " + (e.ExceptionObject as Exception)); };
        AppDomain.CurrentDomain.ProcessExit += delegate { Log("EXIT: 进程结束"); };

        try
        {
            RunMain(args);
        }
        catch (Exception ex)
        {
            Log("MAIN 崩溃: " + ex);
            string logPath = LogDir();
            try { Process.Start("notepad.exe", logPath); } catch { }
            try
            {
                MessageBox.Show("启动时出错（已用记事本打开 " + logPath + "）：\n\n" + ex.Message,
                                TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }

    static string Join(string[] a)
    {
        if (a == null || a.Length == 0) return "(none)";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append('|'); sb.Append(a[i]); }
        return sb.ToString();
    }

    /* ================= 加固：拒收非微软签名的注入 DLL ================= */
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetProcessMitigationPolicy(int policy, ref uint buffer, IntPtr size);

    const int ProcessExtensionPointDisablePolicy = 6;   // 禁用 AppInit_DLLs 等扩展点
    const int ProcessSignaturePolicy = 8;               // 只允许微软签名的映像

    static void Harden(string mode)
    {
        if (mode == "none") { Log("H: 跳过加固"); return; }
        bool wantFull = (mode == null || mode == "full");
        try
        {
            if (wantFull)
            {
                uint sig = 1;   // MicrosoftSignedOnly
                bool ok = SetProcessMitigationPolicy(ProcessSignaturePolicy, ref sig, (IntPtr)4);
                Log("H1: 仅允许微软签名 DLL = " + ok + (ok ? "" : " (err=" + Marshal.GetLastWin32Error() + ")"));
            }
            else Log("H1: 跳过（--harden=ext）");
        }
        catch (Exception e) { Log("H1: 失败 " + e.Message); }

        try
        {
            uint ext = 1;   // DisableExtensionPoints
            bool ok = SetProcessMitigationPolicy(ProcessExtensionPointDisablePolicy, ref ext, (IntPtr)4);
            Log("H2: 禁用扩展点注入 = " + ok + (ok ? "" : " (err=" + Marshal.GetLastWin32Error() + ")"));
        }
        catch (Exception e) { Log("H2: 失败 " + e.Message); }
    }

    /* 列出外部注入的模块（便于确认是哪个 DLL 在捣乱） */
    static void LogModules()
    {
        try
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            StringBuilder sb = new StringBuilder();
            int n = 0;
            foreach (ProcessModule m in Process.GetCurrentProcess().Modules)
            {
                string f = m.FileName;
                if (string.IsNullOrEmpty(f)) continue;
                if (f.StartsWith(exeDir, StringComparison.OrdinalIgnoreCase)) continue;
                if (f.StartsWith(@"C:\Windows\", StringComparison.OrdinalIgnoreCase)) continue;
                sb.Append(f).Append(" ; ");
                n++;
                if (n >= 8) break;
            }
            if(n == 0) Log("H3: 没有发现外部注入模块");
            else Log("H3: 外部模块 = " + sb.ToString());
        }
        catch (Exception e) { Log("H3: 枚举失败 " + e.Message); }
    }

    static void Log(string msg)
    {
        try
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine;
            try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log"), line); } catch { }
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "HeroGridBoard-startup.log"), line); } catch { }
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HeroGridBoard");
            try { Directory.CreateDirectory(dir); File.AppendAllText(Path.Combine(dir, "startup.log"), line); } catch { }
        }
        catch { }
    }

    static string LogDir()
    {
        string exeLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log");
        try { if (File.Exists(exeLog)) return exeLog; } catch { }
        string tmpLog = Path.Combine(Path.GetTempPath(), "HeroGridBoard-startup.log");
        try { if (File.Exists(tmpLog)) return tmpLog; } catch { }
        return exeLog;
    }

    static void RunMain(string[] args)
    {
        try { SetProcessDPIAware(); Log("B: DPI aware ok"); }
        catch (Exception e) { Log("B: DPI FAIL " + e.Message); }

        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string html = Path.Combine(dir, "index.html");
        Log("C: dir=" + dir + "  htmlExists=" + File.Exists(html));

        /* ---- 自检分支 ---- */
        string testPath = ValueOf(args, "--test-capture");
        if (testPath != null) { SelfTestCapture(testPath); return; }
        if (Has(args, "--selftest")) { SelfTestHotkey(); return; }

        if (!File.Exists(html))
        {
            Log("C: 缺 index.html");
            MessageBox.Show("找不到 index.html。\n请把本 exe 和 index.html 放在同一个目录里。", TITLE,
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool serve = Has(args, "--serve");
        serveMode = serve;
        bool noTray = Has(args, "--no-tray");
        bool diag = Has(args, "--diag");
        bool forceBrowser = Has(args, "--browser");
        string shotPath = ValueOf(args, "--shot");

        // 上次内嵌内核没起来（进程被带走/崩溃），这次直接用浏览器模式
        string pendingMark = Path.Combine(dir, ".webview-pending");
        try
        {
            if (File.Exists(pendingMark) && shotPath == null && !forceBrowser)
            {
                Log("W: 发现上次中断标记，本次直接用浏览器模式");
                forceBrowser = true;
                File.Delete(pendingMark);
            }
        }
        catch { }

        bool first = true;
        try
        {
            string inst = FlagValue(args, "--instance");
            if (inst == null || inst.Length == 0) inst = "HeroGridBoard.v5.Instance";
            single = new Mutex(true, inst, out first);
            Log("D: mutex ok, first=" + first);
        }
        catch (Exception e) { Log("D: mutex FAIL " + e.Message); }

        if (!first && shotPath == null && !diag)
        {
            bool act = ActivateExistingWindow();
            Log("D: 已有实例在运行（activate=" + act + "），本进程直接退出");
            return;
        }

        if (serve)
        {
            try { StartServer(dir); Log("E: 局域网服务已开 " + PORT); }
            catch (Exception e) { Log("E: 局域网服务失败 " + e.Message); }
        }

        if (forceBrowser) { RunBrowserMode(html, args, noTray); return; }

        /* ---- 原生窗口 ---- */
        form = new AppForm(html, shotPath, diag);
        if (diag) { try { form.Text = TITLE + " [诊断模式 3 秒后自动关闭]"; } catch { } }
        form.ShotDone += delegate(string path, bool ok)
        {
            Log("SHOT: " + (ok ? "ok " : "fail ") + path);
        };

        if (!noTray && shotPath == null)
        {
            RegisterHotkeyBest();
            CreateTray();
            if (tray != null)
            {
                if (hotkeyLabel != null)
                    tray.ShowBalloonTip(4000, TITLE, "托盘里可退出。按 " + hotkeyLabel + " 随时框选；关窗口＝退出，最小化＝继续后台。", ToolTipIcon.Info);
                else
                    tray.ShowBalloonTip(4000, TITLE, "全局热键都被占用了，可在托盘菜单里手动截图。", ToolTipIcon.Warning);
                if (serve)
                    tray.ShowBalloonTip(4000, TITLE, "局域网：http://" + LocalIPv4() + ":" + PORT + "/", ToolTipIcon.Info);
            }
        }

        if (diag)
        {
            System.Windows.Forms.Timer tmr = new System.Windows.Forms.Timer();
            tmr.Interval = 3000;
            tmr.Tick += delegate { tmr.Stop(); Log("J: diag 关闭"); form.Close(); };
            tmr.Start();
        }

        Log("J: 显示窗口");
        try { Application.Run(form); }
        catch (Exception e) { Log("J: 消息循环异常 " + e); }
        Log("K: 消息循环结束（窗口已关闭）");
    }

    /* ================= 热键（带备用组合） ================= */
    static string hotkeyLabel = null;

    static void RegisterHotkeyBest()
    {
        int[] mods = new int[] { 0x0001 | 0x0002, 0x0001 | 0x0002, 0x0001 | 0x0002 };
        int[] keys = new int[] { 0x41, 0x42, 0x60 };          // A / B / 小键盘0
        string[] names = new string[] { "Ctrl+Alt+A", "Ctrl+Alt+B", "Ctrl+Alt+小键盘0" };

        for (int i = 0; i < keys.Length; i++)
        {
            try
            {
                if (RegisterHotKey(form.Handle, HOTKEY_ID, mods[i], keys[i]))
                {
                    hotkeyLabel = names[i];
                    Log("I: 热键 = " + names[i]);
                    return;
                }
            }
            catch (Exception e) { Log("I: 热键 " + names[i] + " 失败 " + e.Message); }
        }
        hotkeyLabel = null;
        Log("I: 热键全部被占用（可能旧版 评价表拼图板.exe 还在托盘里，先去退出）");
    }

    /* ================= 托盘 ================= */
    static void CreateTray()
    {
        try
        {
            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = TITLE;
            tray.Visible = true;

            ContextMenu menu = new ContextMenu();
            menu.MenuItems.Add(new MenuItem("框选截图  (" + (hotkeyLabel == null ? "未注册" : hotkeyLabel) + ")", delegate { OnHotkey(); }));
            menu.MenuItems.Add(new MenuItem("显示窗口", delegate { ShowWindow(); }));
            menu.MenuItems.Add(new MenuItem("复制局域网地址", delegate { CopyShareUrl(); }));
            menu.MenuItems.Add("-");
            menu.MenuItems.Add(new MenuItem("退出", delegate { Quit(); }));
            tray.ContextMenu = menu;
            tray.DoubleClick += delegate { ShowWindow(); };
            Log("H: 托盘就绪");
        }
        catch (Exception e) { Log("H: 托盘失败 " + e); }
    }

    static void ShowWindow()
    {
        try
        {
            if (form == null) return;
            form.Show();
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.Activate();
            SetForegroundWindow(form.Handle);
        }
        catch { }
    }

    static void Quit()
    {
        try { if (form != null && form.IsHandleCreated) UnregisterHotKey(form.Handle, HOTKEY_ID); } catch { }
        try { if (tray != null) { tray.Visible = false; tray.Dispose(); } } catch { }
        try { if (form != null) form.Close(); } catch { }
        try { Application.Exit(); } catch { }
    }

    /* ================= 截图 → 画板 ================= */
    static void OnHotkey()
    {
        if (capturing) return;
        capturing = true;
        try
        {
            Log("HOTKEY: 框选开始");
            Rectangle region = Rectangle.Empty;
            using (CaptureForm cf = new CaptureForm())
            {
                cf.ShowDialog();
                if (cf.Ok) region = cf.Result;
            }
            if (region.Width < 3 || region.Height < 3) { Log("HOTKEY: 取消"); return; }

            byte[] png;
            using (Bitmap bmp = new Bitmap(region.Width, region.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(region.Left, region.Top, 0, 0, new Size(region.Width, region.Height));
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    png = ms.ToArray();
                }
                try { Clipboard.SetImage(bmp); } catch { }     // 兜底：也在剪贴板里放一份
            }
            Log("HOTKEY: 抓到 " + region.Width + "x" + region.Height + " (" + png.Length + " B)");

            ShowWindow();
            if (form != null && form.WebViewReady)
            {
                string dataUrl = "data:image/png;base64," + Convert.ToBase64String(png);
                form.PostToPage(dataUrl);
                Log("HOTKEY: 已推送到页面");
            }
            else
            {
                TriggerPaste();
            }
        }
        catch (Exception ex)
        {
            Log("HOTKEY: 失败 " + ex);
            if (tray != null) tray.ShowBalloonTip(2500, TITLE, "截图失败：" + ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            capturing = false;
        }
    }

    static void TriggerPaste()
    {
        Thread t = new Thread(delegate()
        {
            Thread.Sleep(220);
            const byte VK_CONTROL = 0x11, VK_V = 0x56;
            const uint KEYUP = 0x0002;
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYUP, UIntPtr.Zero);
        });
        t.IsBackground = true;
        t.Start();
    }

    static bool ActivateExistingWindow()
    {
        IntPtr found = IntPtr.Zero;
        try
        {
            EnumWindows(delegate(IntPtr h, IntPtr p)
            {
                if (!IsWindowVisible(h)) return true;
                StringBuilder sb = new StringBuilder(512);
                GetWindowText(h, sb, sb.Capacity);
                string t = sb.ToString();
                if (t == TITLE || t.StartsWith(TITLE, StringComparison.Ordinal)) { found = h; return false; }
                return true;
            }, IntPtr.Zero);
        }
        catch { return false; }
        if (found == IntPtr.Zero) return false;
        if (IsIconic(found)) ShowWindow(found, 9);   // SW_RESTORE
        return SetForegroundWindow(found);
    }

    static void CopyShareUrl()
    {
        try
        {
            Clipboard.SetText("http://" + LocalIPv4() + ":" + PORT + "/");
            if (tray != null) tray.ShowBalloonTip(2000, TITLE, "已复制局域网地址（需 --serve 启动）", ToolTipIcon.Info);
        }
        catch { }
    }

    /* ================= 自检 ================= */
    static void SelfTestCapture(string outPath)
    {
        try
        {
            Rectangle r = new Rectangle(0, 0, 120, 80);
            using (Bitmap bmp = new Bitmap(r.Width, r.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(r.Width, r.Height));
                bmp.Save(outPath, ImageFormat.Png);
            }
            Log("T: capture ok");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Log("T: capture FAIL " + ex);
            Environment.Exit(1);
        }
    }

    /* ================= 自检：热键 ================= */
    class ProbeForm : Form
    {
        public ProbeForm() { FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; Bounds = new Rectangle(-4000, -4000, 1, 1); }
    }

    static void SelfTestHotkey()
    {
        try
        {
            using (ProbeForm f = new ProbeForm())
            {
                bool ok = RegisterHotKey(f.Handle, HOTKEY_ID, 0x0001 | 0x0002, 0x41);
                if (ok) UnregisterHotKey(f.Handle, HOTKEY_ID);
                Log("S: handle=" + f.Handle.ToString() + " hotkey=" + ok);
            }
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Log("S: FAIL " + ex);
            Environment.Exit(1);
        }
    }

    /* ================= 旧模式：Chrome/Edge 应用窗口 ================= */
    static void RunBrowserMode(string html, string[] args, bool noTray)
    {
        Log("BR: 使用浏览器模式");
        string url = "file:///" + html.Replace('\\', '/');
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string profile = Has(args, "--portable") ? PickProfile(exeDir) : null;

        string[] candidates = new string[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),   @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),   @"Microsoft\Edge\Application\msedge.exe")
        };
        string browser = null;
        for (int i = 0; i < candidates.Length; i++)
            if (File.Exists(candidates[i])) { browser = candidates[i]; break; }

        if (browser == null) { Log("BR: 没有浏览器，交给系统"); Process.Start(url); return; }

        string argline = "--app=\"" + url + "\" --window-size=1520,980 --no-first-run --no-default-browser-check";
        if (profile != null) argline += " --user-data-dir=\"" + profile + "\"";

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(browser);
            psi.Arguments = argline;
            psi.UseShellExecute = false;
            Process.Start(psi);
            Log("BR: ok");
        }
        catch (Exception e) { Log("BR: fail " + e.Message); try { Process.Start(url); } catch { } }

        if (noTray) return;
        CreateTray();
        if (Has(args, "--diag"))
        {
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 3000;
            t.Tick += delegate { t.Stop(); Log("BR: diag 关闭"); Application.Exit(); };
            t.Start();
        }
        Application.Run(new ProbeForm());
    }

    /* 用 Chrome/Edge 的应用窗口打开画板（返回是否成功） */
    public static bool OpenBrowserWindow(string html)
    {
        try
        {
            string url = "file:///" + html.Replace('\\', '/');
            string[] candidates = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),   @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),   @"Microsoft\Edge\Application\msedge.exe")
            };
            string browser = null;
            for (int i = 0; i < candidates.Length; i++)
                if (File.Exists(candidates[i])) { browser = candidates[i]; break; }
            if (browser == null)
            {
                ProcessStartInfo psi = new ProcessStartInfo(url);
                psi.UseShellExecute = true;
                Process.Start(psi);
                return true;
            }
            ProcessStartInfo p2 = new ProcessStartInfo(browser);
            p2.Arguments = "--app=\"" + url + "\" --window-size=1520,980 --no-first-run --no-default-browser-check";
            p2.UseShellExecute = false;
            Process.Start(p2);
            return true;
        }
        catch (Exception e) { Log("W: 打开浏览器窗口失败 " + e.Message); return false; }
    }
    static string PickProfile(string exeDir)
    {
        string[] trials = new string[]
        {
            Path.Combine(exeDir, ".profile"),
            Path.Combine(Path.GetTempPath(), "HeroGridBoard-profile"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HeroGridBoard", "profile")
        };
        for (int i = 0; i < trials.Length; i++)
        {
            try
            {
                Directory.CreateDirectory(trials[i]);
                string probe = Path.Combine(trials[i], ".wtest");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                Log("P: 配置目录 = " + trials[i]);
                return trials[i];
            }
            catch (Exception e) { Log("P: 不可用 " + trials[i] + " -> " + e.Message); }
        }
        return null;
    }

    /* ================= 框选窗体 ================= */
    class CaptureForm : Form
    {
        Point start;
        Rectangle sel = Rectangle.Empty;
        bool dragging;
        public bool Ok = false;
        public Rectangle Result = Rectangle.Empty;

        public CaptureForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            BackColor = Color.Black;
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); Activate(); Focus(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Ok = false; Close(); }
            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) { Ok = false; Close(); return; }
            dragging = true;
            start = e.Location;
            sel = new Rectangle(start, Size.Empty);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!dragging) return;
            Point p = e.Location;
            sel = Rectangle.FromLTRB(Math.Min(start.X, p.X), Math.Min(start.Y, p.Y),
                                     Math.Max(start.X, p.X), Math.Max(start.Y, p.Y));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!dragging) return;
            dragging = false;
            if (sel.Width < 4 || sel.Height < 4) { Ok = false; Close(); return; }
            Ok = true;
            Point vs = SystemInformation.VirtualScreen.Location;
            Result = new Rectangle(vs.X + sel.X, vs.Y + sel.Y, sel.Width, sel.Height);
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(115, 0, 0, 0)))
            {
                if (sel.Width > 0 && sel.Height > 0)
                {
                    Region old = g.Clip;
                    using (Region r = new Region(ClientRectangle))
                    {
                        r.Exclude(sel);
                        g.Clip = r;
                        g.FillRectangle(dim, ClientRectangle);
                    }
                    g.Clip = old;
                }
                else g.FillRectangle(dim, ClientRectangle);
            }

            if (sel.Width > 0 && sel.Height > 0)
            {
                using (Pen pen = new Pen(Color.FromArgb(110, 168, 254), 1))
                    g.DrawRectangle(pen, sel);
                string info = sel.Width + " × " + sel.Height + "   （松手完成 · Esc 取消）";
                using (Font f = new Font("Microsoft YaHei UI", 9f))
                using (SolidBrush tb = new SolidBrush(Color.White))
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
                {
                    SizeF sz = g.MeasureString(info, f);
                    float tx = sel.X + 4;
                    float ty = sel.Y + sel.Height + 6;
                    if (ty > ClientRectangle.Height - 24) ty = sel.Y - sz.Height - 6;
                    g.FillRectangle(bg, tx - 3, ty - 1, sz.Width + 6, sz.Height);
                    g.DrawString(info, f, tb, tx, ty);
                }
            }
        }
    }

    /* ================= 主窗口（WebView2） ================= */
    public class AppForm : Form
    {
        readonly string htmlPath;
        readonly string shotPath;
        readonly bool diagMode;
        Panel host;
        CoreWebView2Controller controller;
        Label loading;
        Panel errPanel;
        Label errLabel;
        Button errBtn;
        public bool WebViewReady = false;
        public event Action<string, bool> ShotDone;

        public AppForm(string html, string shot, bool diag)
        {
            htmlPath = html; shotPath = shot; diagMode = diag;

            Text = TITLE;
            ClientSize = new Size(1500, 940);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(720, 460);
            BackColor = Color.FromArgb(244, 238, 227);
            DoubleBuffered = true;
            try { Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico")); } catch { }

            host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.FromArgb(244, 238, 227);
            Controls.Add(host);

            loading = new Label();
            loading.Text = "正在启动内嵌内核…";
            loading.Dock = DockStyle.Fill;
            loading.TextAlign = ContentAlignment.MiddleCenter;
            loading.ForeColor = Color.FromArgb(120, 110, 95);
            host.Controls.Add(loading);

            // 内核起不来时用这个面板兜底（不自动拉 explorer，免得弹 0xc0000142）
            errPanel = new Panel();
            errPanel.Dock = DockStyle.Fill;
            errPanel.BackColor = Color.FromArgb(250, 246, 238);
            errPanel.Visible = false;

            errLabel = new Label();
            errLabel.Dock = DockStyle.Fill;
            errLabel.TextAlign = ContentAlignment.MiddleCenter;
            errLabel.ForeColor = Color.FromArgb(90, 82, 70);
            errLabel.Text = "";

            errBtn = new Button();
            errBtn.Text = "用浏览器打开同一份画板";
            errBtn.AutoSize = true;
            errBtn.Click += delegate { OpenInBrowser(); };

            errPanel.Controls.Add(errLabel);
            errPanel.Controls.Add(errBtn);
            errPanel.Resize += delegate
            {
                errBtn.Location = new Point(Math.Max(8, (errPanel.Width - errBtn.Width) / 2),
                                            Math.Max(8, (errPanel.Height + 90) / 2));
            };
            Controls.Add(errPanel);
            errPanel.BringToFront();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            SyncWebViewBounds();
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
            SyncWebViewBounds();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            StartWatchdog();
            InitWebView();
        }

        void StartWatchdog()
        {
            if (shotPath != null) return;
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 9000;
            t.Tick += delegate
            {
                t.Stop();
                if (WebViewReady) return;
                Log("W: 9 秒未就绪，判定内嵌内核不可用");
                try { File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".webview-pending")); } catch { }
                ShowFallback("内嵌内核 9 秒没有响应。");
            };
            t.Start();
        }

        bool syncing = false;

        void SyncWebViewBounds()
        {
            if (syncing) return;          // 防重入：Bounds 变化可能再次触发布局
            syncing = true;
            try
            {
                if (controller == null) return;
                controller.Bounds = new Rectangle(0, 0, Math.Max(1, host.Width), Math.Max(1, host.Height));
                if (Visible) controller.NotifyParentWindowPositionChanged();
            }
            catch (Exception e) { Log("W7: 同步尺寸失败 " + e.Message); }
            finally { syncing = false; }
        }

        /* 内核不可用时的兜底界面：说明 + 一个手动按钮 */
        void ShowFallback(string why)
        {
            try
            {
                errLabel.Text = why + "\r\n\r\n画板本身没问题，只是这个窗口的内核没起来。\r\n" +
                                "点下面的按钮用默认浏览器打开同一份画板；详细信息见 startup.log。";
                errPanel.Visible = true;
                errPanel.BringToFront();
                loading.Visible = false;
            }
            catch { }
            Log("W: 显示兜底界面：" + why);
        }

        void OpenInBrowser()
        {
            string url = "file:///" + htmlPath.Replace('\\', '/');
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(url);
                psi.UseShellExecute = true;
                Process.Start(psi);
                Log("W: 已用默认浏览器打开");
            }
            catch (Exception e) { Log("W: 打开浏览器失败 " + e.Message); }
        }

        async void InitWebView()
        {
            try
            {
                Log("W0: apartment=" + Thread.CurrentThread.GetApartmentState() + "  syncCtx=" + (SynchronizationContext.Current != null) + "  threadId=" + Thread.CurrentThread.ManagedThreadId);
                string folder = PickUserDataFolder();
                Log("W1: userData=" + folder);
                string mark = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".webview-pending");
                try { File.WriteAllText(mark, DateTime.Now.ToString("s")); } catch { }

                controller = await CreateController(folder);
                if (controller == null) throw new Exception("内嵌内核控制器创建失败（数据目录可能被占用）");
                try { File.Delete(mark); } catch { }
                controller.Bounds = new Rectangle(0, 0, Math.Max(1, host.Width), Math.Max(1, host.Height));

                CoreWebView2 wv = controller.CoreWebView2;

                wv.Settings.AreDefaultContextMenusEnabled = false;
                wv.Settings.AreDevToolsEnabled = false;
                wv.Settings.IsStatusBarEnabled = false;
                wv.Settings.IsZoomControlEnabled = false;
                wv.Settings.IsBuiltInErrorPageEnabled = true;
                wv.Settings.IsWebMessageEnabled = true;

                wv.NewWindowRequested += delegate(object s, CoreWebView2NewWindowRequestedEventArgs e) { e.Handled = true; };
                wv.ProcessFailed += delegate(object s, CoreWebView2ProcessFailedEventArgs e)
                {
                    Log("W8: 渲染进程失败 kind=" + e.ProcessFailedKind);
                };
                wv.WebMessageReceived += delegate(object s, CoreWebView2WebMessageReceivedEventArgs e)
                {
                    try { string m = e.TryGetWebMessageAsString(); if (m != null && m.Length <= 4000) Log("JS: " + m); }
                    catch { }
                };
                wv.NavigationCompleted += delegate(object s, CoreWebView2NavigationCompletedEventArgs e)
                {
                    Log("W4: 导航完成 ok=" + e.IsSuccess + " err=" + e.WebErrorStatus);
                    if (e.IsSuccess)
                    {
                        WebViewReady = true;
                        try { loading.Visible = false; } catch { }
                        if (shotPath != null) TakeShotLater();
                        else if (diagMode) ProbePage();
                    }
                };

                string url = "file:///" + htmlPath.Replace('\\', '/');
                Log("W5: navigate " + url);
                wv.Navigate(url);
            }
            catch (Exception ex)
            {
                try { File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".webview-pending")); } catch { }
                Log("W: 内嵌内核失败 " + ex.Message);
                try { loading.Text = "内嵌内核启动失败：" + ex.Message + "\r\n将改用浏览器窗口打开。"; } catch { }
                FallbackToBrowser();
            }
        }

        async Task<CoreWebView2Controller> CreateController(string folder)
        {
            try
            {
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, folder, null);
                Log("W2: env ok, browser=" + env.BrowserVersionString + "  udf=" + folder + "  apt=" + Thread.CurrentThread.GetApartmentState());
                CoreWebView2Controller c = await env.CreateCoreWebView2ControllerAsync(host.Handle);
                Log("W3: controller ok");
                return c;
            }
            catch (Exception e)
            {
                Log("W3b: 控制器创建失败 " + folder + " -> " + e.Message);
                return null;
            }
        }

        void FallbackToBrowser()
        {
            // 内核起不来：直接把画板用浏览器应用窗口打开，自己的空窗口藏起来。
            // 托盘和全局热键仍在（截图走剪贴板 + 自动切换窗口粘贴）。
            bool ok = Launcher.OpenBrowserWindow(htmlPath);
            Log("W: 回退到浏览器窗口 ok=" + ok);
            if (ok)
            {
                if (Launcher.serveMode)
                {
                    // --serve 模式：进程要继续活着才能提供局域网服务
                    Log("W: 回退成功；--serve 模式保留进程（窗口隐藏）");
                    try { Hide(); } catch { }
                }
                else
                {
                    // 普通模式：不再把自己藏进托盘（会变成“看不见的进程”），直接退出
                    Log("W: 回退成功，本进程退出");
                    try { Close(); } catch { }
                }
            }
            else
            {
                ShowFallback("内嵌内核启动失败，浏览器窗口也没能打开。");
            }
        }

        void ProbePage()
        {
            try
            {
                Task<string> t = controller.CoreWebView2.ExecuteScriptAsync("document.title + ' | cells=' + document.querySelectorAll('.cell').length + ' | tabs=' + document.querySelectorAll('.tab').length");
                t.ContinueWith(delegate(Task<string> x)
                {
                    Log("W6: 页面自检 " + (x.Exception == null ? x.Result : x.Exception.Message));
                });
            }
            catch (Exception e) { Log("W6: 自检失败 " + e.Message); }
        }

        void TakeShotLater()
        {
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 1500;
            t.Tick += delegate
            {
                t.Stop();
                CaptureAndExit();
            };
            t.Start();
        }

        async void CaptureAndExit()
        {
            try
            {
                string path = shotPath;
                using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    await controller.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
                }
                Log("SHOT: 已写 " + path);
                if (ShotDone != null) ShotDone(path, true);
                Close();
            }
            catch (Exception ex)
            {
                Log("SHOT: 失败 " + ex.Message);
                if (ShotDone != null) ShotDone(shotPath, false);
                Close();
            }
        }

        public void PostToPage(string dataUrl)
        {
            try
            {
                if (controller != null && controller.CoreWebView2 != null)
                    controller.CoreWebView2.PostWebMessageAsString(dataUrl);
            }
            catch (Exception e) { Log("POST: 失败 " + e.Message); }
        }

        static string PickUserDataFolder()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] trials = new string[]
            {
                Path.Combine(exeDir, ".webview"),
                Path.Combine(Path.GetTempPath(), "HeroGridBoard-webview"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HeroGridBoard", "webview")
            };
            for (int i = 0; i < trials.Length; i++)
            {
                try
                {
                    Directory.CreateDirectory(trials[i]);
                    string probe = Path.Combine(trials[i], ".wtest");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    return trials[i];
                }
                catch (Exception e) { Log("W0: 数据目录不可用 " + trials[i] + " -> " + e.Message); }
            }
            return Path.Combine(Path.GetTempPath(), "HeroGridBoard-webview");
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                OnHotkey();
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Log("FORM: 关闭 reason=" + e.CloseReason);
            try { if (controller != null) { controller.Close(); controller = null; } } catch { }
            base.OnFormClosing(e);
        }
    }

    /* ================= 极简静态服务器 ================= */
    static void StartServer(string root)
    {
        TcpListener listener = new TcpListener(IPAddress.Any, PORT);
        listener.Start();
        string rootFull = Path.GetFullPath(root);

        Thread th = new Thread(delegate()
        {
            while (true)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch { return; }
                ThreadPool.QueueUserWorkItem(delegate(object state) { Handle((TcpClient)state, rootFull); }, client);
            }
        });
        th.IsBackground = true;
        th.Start();
    }

    static int activeConns = 0;
    const int MAX_CONNS = 8;
    const int MAX_LINE = 2048;

    // 只对外提供画板需要的静态资源，其它一律 404（避免把 startup.log / 存档泄露出去）
    static bool AllowedExt(string f)
    {
        string e = Path.GetExtension(f).ToLowerInvariant();
        return e == ".html" || e == ".js" || e == ".css" || e == ".png" || e == ".ico"
            || e == ".webmanifest" || e == ".json" || e == ".svg" || e == ".jpg" || e == ".jpeg";
    }

    static string ReadLineBounded(Stream st, int max)
    {
        StringBuilder sb = new StringBuilder();
        int b;
        while ((b = st.ReadByte()) >= 0)
        {
            if (b == 10) break;
            if (b == 13) continue;
            if (sb.Length >= max) throw new IOException("line too long");
            sb.Append((char)b);
        }
        return sb.ToString();
    }

    static void Handle(TcpClient client, string rootFull)
    {
        if (Interlocked.Increment(ref activeConns) > MAX_CONNS)
        {
            Interlocked.Decrement(ref activeConns);
            try { client.Close(); } catch { }
            return;
        }
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 5000;
                NetworkStream ns = client.GetStream();

                string requestLine = ReadLineBounded(ns, MAX_LINE);
                if (string.IsNullOrEmpty(requestLine)) return;
                string[] parts = requestLine.Split(' ');
                if (parts.Length < 2) return;
                string method = parts[0];
                string path = parts[1];
                if (method != "GET" && method != "HEAD") { SendText(ns, 405, "method not allowed"); return; }

                int guard = 0;
                while (guard++ < 60)
                {
                    string h = ReadLineBounded(ns, MAX_LINE);
                    if (string.IsNullOrEmpty(h)) break;
                }

                int q = path.IndexOf('?');
                if (q >= 0) path = path.Substring(0, q);
                try { path = Uri.UnescapeDataString(path); } catch { SendText(ns, 400, "bad request"); return; }
                if (path.IndexOf("..", StringComparison.Ordinal) >= 0) { SendText(ns, 403, "forbidden"); return; }
                if (path == "/" || path.Length == 0) path = "/index.html";

                string rootPrefix = Path.GetFullPath(rootFull).TrimEnd('\\') + "\\";
                string full = Path.GetFullPath(Path.Combine(rootPrefix, path.TrimStart('/').Replace('/', '\\')));
                if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) { SendText(ns, 403, "forbidden"); return; }
                if (!AllowedExt(full)) { SendText(ns, 404, "not found"); return; }
                if (!File.Exists(full)) { SendText(ns, 404, "not found"); return; }

                byte[] body = File.ReadAllBytes(full);
                byte[] head = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\nContent-Type: " + MimeOf(full) + "\r\n" +
                    "Content-Length: " + body.Length + "\r\n" +
                    "X-Content-Type-Options: nosniff\r\n" +
                    "Cache-Control: no-store\r\nConnection: close\r\n\r\n");
                ns.Write(head, 0, head.Length);
                if (method == "GET") ns.Write(body, 0, body.Length);
                ns.Flush();
            }
        }
        catch { }
        finally { Interlocked.Decrement(ref activeConns); }
    }

    static void SendText(NetworkStream ns, int code, string text)
    {
        byte[] body = Encoding.UTF8.GetBytes(text);
        byte[] head = Encoding.ASCII.GetBytes(
            "HTTP/1.1 " + code + " Error\r\nContent-Type: text/plain; charset=utf-8\r\n" +
            "Content-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
        ns.Write(head, 0, head.Length);
        ns.Write(body, 0, body.Length);
        ns.Flush();
    }

    static string MimeOf(string file)
    {
        switch (Path.GetExtension(file).ToLowerInvariant())
        {
            case ".html": return "text/html; charset=utf-8";
            case ".js": return "application/javascript; charset=utf-8";
            case ".css": return "text/css; charset=utf-8";
            case ".json": return "application/json; charset=utf-8";
            case ".webmanifest": return "application/manifest+json; charset=utf-8";
            case ".png": return "image/png";
            case ".jpg":
            case ".jpeg": return "image/jpeg";
            case ".svg": return "image/svg+xml";
            case ".ico": return "image/x-icon";
            default: return "application/octet-stream";
        }
    }

    static string LocalIPv4()
    {
        try
        {
            IPAddress[] list = Dns.GetHostAddresses(Dns.GetHostName());
            for (int i = 0; i < list.Length; i++)
            {
                IPAddress a = list[i];
                if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                    return a.ToString();
            }
        }
        catch { }
        return "127.0.0.1";
    }

    static bool Has(string[] args, string flag)
    {
        if (args == null) return false;
        for (int i = 0; i < args.Length; i++)
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static string FlagValue(string[] args, string flag)
    {
        string v = ValueOf(args, flag);
        if (v != null) return v;
        if (args != null)
        {
            string pre = flag + "=";
            for (int i = 0; i < args.Length; i++)
                if (args[i] != null && args[i].StartsWith(pre, StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring(pre.Length);
        }
        return null;
    }

    static string ValueOf(string[] args, string flag)
    {
        if (args == null) return null;
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }
}
