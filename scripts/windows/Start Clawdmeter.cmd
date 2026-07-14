@echo off
setlocal EnableExtensions EnableDelayedExpansion

for %%I in ("%~dp0..\..") do set "REPO_ROOT=%%~fI"
cd /d "%REPO_ROOT%"

set "PYTHON_EXE=%REPO_ROOT%\.venv\Scripts\python.exe"
set "REQ_FILE=daemon\requirements-windows.txt"
set "TRAY_SCRIPT=%REPO_ROOT%\daemon\tray_windows.py"
set "UV_CACHE_DIR=%REPO_ROOT%\.codex-tmp\uv-cache"
set "UV_PYTHON_INSTALL_DIR=%REPO_ROOT%\.codex-tmp\uv-python"
set "UV_EXE="

set "CLAWDMETER_CONFIG_DIR=%USERPROFILE%\.config\clawdmeter"
set "GO_CRED_FILE=%CLAWDMETER_CONFIG_DIR%\opencode-go-credentials.json"
set "TRAY_CONFIG_DIR=%LOCALAPPDATA%\Clawdmeter"
set "TRAY_CONFIG=%TRAY_CONFIG_DIR%\config.json"

rem ── Check for --setup / -s flag ──────────────────────────────────────────
set "FORCE_SETUP="
for %%a in (%*) do (
    if /i "%%a"=="--setup" set "FORCE_SETUP=1"
    if /i "%%a"=="/setup" set "FORCE_SETUP=1"
    if /i "%%a"=="-s" set "FORCE_SETUP=1"
)

rem ── Provider setup wizard (only when --setup) ──────────────────────────

:setup_prompt
if defined FORCE_SETUP goto :show_setup_menu

rem Read the provider straight out of the JSON file (no grep/findstr/choice
rem involved — the old auto-detect path was fragile on different cmd hosts
rem and a hang-prone `choice` could lock the window). Use a simple for /f
rem that splits on the colon after the provider key.
set "CURRENT_PROVIDER="
if exist "%TRAY_CONFIG%" (
    for /f "usebackq tokens=1* delims=:" %%a in ("%TRAY_CONFIG%") do (
        set "LINE=%%a"
        call :_parse_provider_line "%%LINE%%"
    )
)
if defined CLAWDMETER_PROVIDER set "CURRENT_PROVIDER=%CLAWDMETER_PROVIDER%"
if /i "%CURRENT_PROVIDER%"=="go" (
    if exist "%GO_CRED_FILE%" (
        echo.
        echo OpenCode Go credentials found.
        echo Press R to refresh auth cookie, or any other key to continue...
        choice /c RN /n /t 5 /d N >nul 2>nul
        if !errorlevel! equ 1 goto :setup_opencode_go
    ) else (
        goto :setup_opencode_go
    )
)
if not defined CURRENT_PROVIDER set "CURRENT_PROVIDER=claude"
goto :after_setup

rem Helper: extract provider value from a line like '"provider": "minimax",'
:_parse_provider_line
set "LINE_NOQUOTES=%~1"
set "LINE_CLEANED=%LINE_NOQUOTES:"=%
for /f "tokens=1* delims=:" %%x in ("%LINE_CLEANED%") do (
    set "KEY=%%x"
    set "VAL=%%y"
    if /i "%KEY: =%"=="provider" set "CURRENT_PROVIDER=%VAL: =%"
)
goto :eof

:check_go_cookie
rem Legacy alias — kept so the :show_setup_menu jump still resolves
rem when --setup was forced. Same logic as the main flow.
set "CURRENT_PROVIDER=claude"
if exist "%TRAY_CONFIG%" (
    for /f "usebackq tokens=1* delims=:" %%a in ("%TRAY_CONFIG%") do (
        set "LINE=%%a"
        call :_parse_provider_line "%%LINE%%"
    )
)
set "CURRENT_PROVIDER=%CURRENT_PROVIDER:"=%
set "CURRENT_PROVIDER=%CURRENT_PROVIDER: =%

if /i "%CURRENT_PROVIDER%"=="go" (
    if exist "%GO_CRED_FILE%" (
        echo.
        echo OpenCode Go credentials found.
        echo Press R to refresh auth cookie, or any other key to continue...
        choice /c RN /n /t 5 /d N >nul 2>nul
        if !errorlevel! equ 1 goto :setup_opencode_go
    ) else (
        goto :setup_opencode_go
    )
)
goto :after_setup

:show_setup_menu
echo.
echo   ====== Clawdmeter Setup ======
echo.
echo   Select your AI provider:
echo.
echo     1) Claude (default — needs claude login)
echo     2) Codex (needs Codex auth)
echo     3) OpenCode Go (needs workspace ID + auth cookie)
echo.
echo     (already configured? just press Enter to skip, or
echo      run "%~nx0 --setup" to force this menu)
echo.
set /p "PROVIDER_CHOICE=Enter choice (1/2/3) [skip]: "
if "!PROVIDER_CHOICE!"=="" goto :after_setup

if "!PROVIDER_CHOICE!"=="3" (
    goto :setup_opencode_go
) else if "!PROVIDER_CHOICE!"=="2" (
    set "CLAWDMETER_PROVIDER=codex"
    echo Setting provider to Codex...
) else (
    set "CLAWDMETER_PROVIDER=claude"
    echo Setting provider to Claude...
)

rem Save provider to tray config
if not exist "%TRAY_CONFIG_DIR%" mkdir "%TRAY_CONFIG_DIR%" >nul 2>nul
echo {"provider": "%CLAWDMETER_PROVIDER%"} > "%TRAY_CONFIG%"
echo Saved provider to tray config.
goto :after_setup

:setup_opencode_go
cls
echo.
echo   ====== OpenCode Go Setup ======
echo.
echo   How to get your credentials:
echo.
echo     1) Go to https://opencode.ai and log in
echo     2) The URL will be: https://opencode.ai/workspace/wrk_.../go
echo        The "wrk_..." part is your Workspace ID
echo     3) Open DevTools (F12) ^> Application ^> Cookies ^>
echo        Copy the "auth" cookie value (starts with Fe26.2**)
echo.
if exist "%GO_CRED_FILE%" (
    echo   [Current credentials found — press Enter to keep or type new value]
    for /f "tokens=2 delims=:" %%w in ('findstr "workspace_id" "%GO_CRED_FILE%"') do (
        for /f "tokens=* delims= " %%a in ("%%~w") do set "OLD_WID=%%a"
    )
    set "OLD_WID=!OLD_WID:"=!
    set "OLD_WID=!OLD_WID:,=!"
)
echo.
set /p "GO_WID=Workspace ID (wrk_...^) [!OLD_WID!]: "
if "!GO_WID!"=="" set "GO_WID=!OLD_WID!"
if not defined GO_WID (
    echo Workspace ID is required.
    pause
    goto :setup_opencode_go
)

set /p "GO_COOKIE=Auth Cookie (Fe26.2**...^): "
if not defined GO_COOKIE (
    echo Auth cookie is required.
    pause
    goto :setup_opencode_go
)

if not exist "%CLAWDMETER_CONFIG_DIR%" mkdir "%CLAWDMETER_CONFIG_DIR%" >nul 2>nul

rem Write credentials file
> "%GO_CRED_FILE%" (
    echo {
    echo   "workspace_id": "%GO_WID%",
    echo   "auth_cookie": "%GO_COOKIE%"
    echo }
)
icacls "%GO_CRED_FILE%" /inheritance:r /grant "%USERNAME%:(R,W)" >nul 2>nul

rem Save provider to tray config
if not exist "%TRAY_CONFIG_DIR%" mkdir "%TRAY_CONFIG_DIR%" >nul 2>nul
echo {"provider": "go"} > "%TRAY_CONFIG%"

echo.
echo OpenCode Go credentials saved to %GO_CRED_FILE%
echo (permissions restricted to current user only)
echo.
echo Tip: when the cookie expires, just run "%~nx0 --setup"

:after_setup
echo.

rem ── Normal startup ──────────────────────────────────────────────────────

call :ensure_python
if errorlevel 1 goto fail

echo Installing or checking Clawdmeter dependencies...
"%PYTHON_EXE%" -m pip --version >nul 2>nul
if errorlevel 1 (
    echo Installing pip into the Clawdmeter environment...
    "%PYTHON_EXE%" -m ensurepip --upgrade
    if errorlevel 1 goto fail
)
"%PYTHON_EXE%" -m pip install --quiet -r "%REQ_FILE%"
if errorlevel 1 goto fail

echo Enabling Start at login...
"%PYTHON_EXE%" -c "import os, sys; sys.path.insert(0, os.getcwd()); import daemon.autostart_windows as a; a.enable(tray_script=os.path.abspath(r'daemon\tray_windows.py'))"
if errorlevel 1 goto fail

echo Starting Clawdmeter tray...
rem Generate a tiny VBScript launcher in %TEMP% that uses WScript.Shell.Run
rem with bHideWindow=0 (SW_HIDE) and bWaitOnReturn=False. VBScript's Run
rem truly detaches the child from the parent's console — it doesn't share
rem the std handle set the way start /B does, so the parent cmd can exit
rem cleanly the moment Run returns. The VBS file lives only for the
rem duration of the spawn, then deletes itself.
set "VBS=%TEMP%\Clawdmeter_launcher_%RANDOM%.vbs"
(
    echo Set WshShell = WScript.CreateObject^("WScript.Shell"^)
    echo WshShell.Run Chr^(34^) ^& "%REPO_ROOT%\.venv\Scripts\pythonw.exe" ^& Chr^(34^) ^& " " ^& Chr^(34^) ^& "%REPO_ROOT%\daemon\tray_windows.py" ^& Chr^(34^), 0, False
    echo Set fso = CreateObject^("Scripting.FileSystemObject"^)
    echo fso.DeleteFile "%VBS%"
) > "%VBS%"
wscript //nologo "%VBS%"
exit /b 0

:find_uv
set "UV_EXE="
for /f "delims=" %%U in ('where uv 2^>nul') do (
    if not defined UV_EXE set "UV_EXE=%%U"
)
if defined UV_EXE exit /b 0
if exist "%USERPROFILE%\.local\bin\uv.exe" (
    set "UV_EXE=%USERPROFILE%\.local\bin\uv.exe"
    set "PATH=%USERPROFILE%\.local\bin;%PATH%"
    exit /b 0
)
exit /b 1

:ensure_uv
call :find_uv
if not errorlevel 1 exit /b 0

echo Installing the small Python helper uv...
echo First run may need internet access and may take a minute.
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { irm https://astral.sh/uv/install.ps1 | iex } catch { exit 1 }"
if errorlevel 1 (
    echo Could not install uv automatically.
    echo Check your internet connection, then run scripts\windows\Start Clawdmeter.cmd again.
    exit /b 1
)

call :find_uv
if errorlevel 1 (
    echo uv installed, but this window cannot find it yet.
    echo Close this window and double-click scripts\windows\Start Clawdmeter.cmd again.
    exit /b 1
)
exit /b 0

:ensure_python
if exist "%PYTHON_EXE%" (
    "%PYTHON_EXE%" -c "import sys" >nul 2>nul
    if not errorlevel 1 exit /b 0
    echo Existing .venv is broken; recreating it...
    rmdir /s /q ".venv" >nul 2>nul
)

call :ensure_uv
if errorlevel 1 exit /b 1

echo Installing/checking Python 3.11...
if not exist ".codex-tmp" mkdir ".codex-tmp" >nul 2>nul
"%UV_EXE%" python install 3.11
if errorlevel 1 exit /b 1

echo Creating Clawdmeter Python environment...
"%UV_EXE%" venv --python 3.11 ".venv"
if errorlevel 1 exit /b 1

"%PYTHON_EXE%" -c "import sys" >nul 2>nul
if errorlevel 1 (
    echo Python environment was created but did not start correctly.
    echo Delete the .venv folder and run scripts\windows\Start Clawdmeter.cmd again.
    exit /b 1
)
exit /b 0

:fail
echo.
echo Clawdmeter could not start. Check the message above, then press any key to close.
pause >nul
exit /b 1
