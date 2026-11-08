@echo off
setlocal
cd /d "%~dp0"

echo Compiling GrandPOS_Deployer.exe...
"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" /target:winexe /out:"GrandPOS_Deployer.exe" /reference:System.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Core.dll "Program.cs"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCCESS] GrandPOS_Deployer.exe compiled successfully!
) else (
    echo.
    echo [ERROR] Compilation failed. If GrandPOS_Deployer.exe is currently running, please close it and try again.
)

pause
endlocal

