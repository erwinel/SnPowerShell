@ECHO OFF
SET ScriptName=%~n0.ps1
PUSHD "%~dp0%"
IF EXIST "%ScriptName%" GOTO :runCmd
ECHO "%~dp0%%ScriptName% does not exist."
GOTO end
:runCmd
MODE CON: COLS=2048
powershell -STA -ExecutionPolicy Bypass -File "%ScriptName%" %*
IF ERRORLEVEL 1 "Script execution returned error code %ERRORLEVEL%."
:end
POPD