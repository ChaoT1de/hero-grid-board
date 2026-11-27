@echo off
chcp 936 >nul
cd /d "%~dp0"
echo ============================================
echo   拼图板 · 启动诊断
echo ============================================
echo.
set "EXE=拼图板.exe"
if not exist "%EXE%" (
  echo [x] 找不到 拼图板.exe
  echo     如果你还在用旧版 评价表拼图板.exe，请先在托盘图标上右键 - 退出
  pause
  exit /b 1
)
if not exist "index.html" (
  echo [x] 找不到 index.html（必须和 exe 放同一目录）
  pause
  exit /b 1
)
del /q startup.log 2>nul
del /q ".webview-pending" 2>nul
echo [1/3] 以 --diag 启动（约 4 秒后自动退出）...
"%EXE%" --diag
echo [2/3] 日志位置：
set "LOG="
if exist "startup.log" set "LOG=%~dp0startup.log"
if not defined LOG if exist "%TEMP%\HeroGridBoard-startup.log" set "LOG=%TEMP%\HeroGridBoard-startup.log"
if not defined LOG (
  echo [x] 没生成日志：exe 在进入程序前就被系统/安全软件拦下了。
  echo     可以先用：拼图板.exe --browser  （旧方式，用 Chrome/Edge 窗口打开）
  pause
  exit /b 1
)
echo      %LOG%
echo [3/3] 用记事本打开日志（把内容发给开发者即可）
start "" notepad "%LOG%"
exit /b 0
