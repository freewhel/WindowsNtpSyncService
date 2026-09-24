@echo off
chcp 65001 >nul
%1 mshta vbscript:CreateObject("Shell.Application").ShellExecute("cmd.exe","/c %~s0 ::","","runas",1)(window.close)&&exit
cd /d "%~dp0"

set "SERVICE_NAME=WindowsNtpSyncService"

echo ==============================================
echo  卸载 NTP时间同步服务
echo ==============================================

::先停止服务
net stop "%SERVICE_NAME%"

::删除服务
sc delete "%SERVICE_NAME%"

if %errorlevel% equ 0 (
    echo 服务卸载成功。
) else (
    echo 卸载失败，服务可能不存在。
)

pause
