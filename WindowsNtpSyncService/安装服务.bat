@echo off
chcp 65001 >nul
%1 mshta vbscript:CreateObject("Shell.Application").ShellExecute("cmd.exe","/c %~s0 ::","","runas",1)(window.close)&&exit
cd /d "%~dp0"

echo ==============================================
echo  NTP时间同步服务安装脚本
echo  当前目录: %~dp0
echo ==============================================

set "SERVICE_NAME=WindowsNtpSyncService"
set "EXE_NAME=WindowsNtpSyncService.exe"

if not exist "%~dp0%EXE_NAME%" (
    echo 错误：未找到 %EXE_NAME% ，请将本bat放在exe同一目录！
    pause
    exit /b 1
)

set "BIN_PATH=%~dp0%EXE_NAME%"

:: 创建服务
sc create "%SERVICE_NAME%" binPath= "\"%BIN_PATH%\"" start= auto obj= LocalSystem
if %errorlevel% neq 0 (
    echo 创建服务失败！
    pause
    exit /b
)

:: 设置服务描述
sc description "%SERVICE_NAME%" "NTP时间自动同步服务，同步阿里云NTP服务器时间"

echo.
echo 服务创建成功！
echo 服务名称：%SERVICE_NAME%
echo 程序路径：%BIN_PATH%
echo.
echo 是否立即启动服务？(Y/N)
set /p choice=
if /i "%choice%"=="Y" (
    net start "%SERVICE_NAME%"
)

echo.
echo 完成。
pause
