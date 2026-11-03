@echo off
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set ROOT=%~dp0
"%CSC%" /nologo /optimize /codepage:65001 /target:winexe /win32icon:"%ROOT%assets\ezCapScreen.ico" /resource:"%ROOT%assets\ezCapScreen.ico",ezCapScreen.ico /resource:"%ROOT%assets\ezCapScreen.png",ezCapScreen.png /out:"%ROOT%ezCapScreen.exe" "%ROOT%src\*.cs" || exit /b 1
"%CSC%" /nologo /optimize /codepage:65001 /target:exe /out:"%ROOT%tests\verify.exe" "%ROOT%tests\verify.cs" || exit /b 1
