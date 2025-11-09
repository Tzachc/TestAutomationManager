@echo off
REM Batch script to completely clean Visual Studio cache and rebuild
echo.
echo ======================================
echo   Cleaning Visual Studio Cache
echo ======================================
echo.

REM Delete .vs folder (IntelliSense database)
if exist ".vs" (
    echo Deleting .vs folder...
    rmdir /s /q ".vs"
)

REM Delete bin folders
for /d /r %%i in (bin) do (
    if exist "%%i" (
        echo Deleting %%i...
        rmdir /s /q "%%i"
    )
)

REM Delete obj folders
for /d /r %%i in (obj) do (
    if exist "%%i" (
        echo Deleting %%i...
        rmdir /s /q "%%i"
    )
)

echo.
echo ======================================
echo   Cleanup Complete!
echo ======================================
echo.
echo Next steps:
echo   1. Open TestAutomationManager.sln in Visual Studio
echo   2. Build -^> Clean Solution
echo   3. Build -^> Rebuild Solution
echo   4. All errors should be gone!
echo.
pause
