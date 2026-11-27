@echo off
chcp 936 >nul
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [x] 找不到 csc.exe，需要 .NET Framework 4 以上
  pause
  exit /b 1
)
set "FAC=C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\netstandard.dll"
if not exist "%FAC%" (
  echo [x] 找不到 .NET Framework 4.8 的 Facades\netstandard.dll
  pause
  exit /b 1
)

echo [1/2] 结束正在运行的旧进程...
taskkill /F /IM "拼图板.exe" >nul 2>&1
taskkill /F /IM "拼图板-new.exe" >nul 2>&1
taskkill /F /IM "拼图板-test.exe" >nul 2>&1
taskkill /F /IM "评价表拼图板.exe" >nul 2>&1
ping -n 2 127.0.0.1 >nul

echo [2/2] 编译...
"%CSC%" /nologo /codepage:65001 /target:winexe /out:"%~dp0拼图板.exe" /win32icon:"%~dp0app.ico" "%~dp0launcher.cs" /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:"%~dp0Microsoft.Web.WebView2.Core.dll" /r:"%FAC%"
if not errorlevel 1 (
  echo [ok] 已生成 拼图板.exe
  del /q "%~dp0拼图板-new.exe" "%~dp0拼图板-test.exe" 2>nul
  pause
  exit /b 0
)

echo [i] 拼图板.exe 还被占用，改输出 拼图板-new.exe
"%CSC%" /nologo /codepage:65001 /target:winexe /out:"%~dp0拼图板-new.exe" /win32icon:"%~dp0app.ico" "%~dp0launcher.cs" /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:"%~dp0Microsoft.Web.WebView2.Core.dll" /r:"%FAC%"
if errorlevel 1 (
  echo [x] 编译失败
  pause
  exit /b 1
)
echo [ok] 已生成 拼图板-new.exe —— 先退出程序，再改名成 拼图板.exe
pause
exit /b 0
