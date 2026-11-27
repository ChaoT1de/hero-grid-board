@echo off
setlocal
set "HTML=%~dp0index.html"
if not exist "%HTML%" (
  echo index.html not found.
  pause
  exit /b 1
)
set "URL=file:///%HTML:\=/%"
for %%B in (
  "%ProgramFiles%\Google\Chrome\Application\chrome.exe"
  "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"
  "%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"
  "%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"
) do (
  if exist %%B (
    start "" %%B --app="%URL%" --window-size=1500,980
    exit /b 0
  )
)
start "" "%HTML%"
exit /b 0
