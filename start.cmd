@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "APP_NAME=LearningHub"
set "PORT=5180"
set "DOTNET_PROJECT=src/LearningHub.Api"

echo.
echo %APP_NAME%  —  start / stop
echo.

:: ---------- Helpers ----------
:where_is tool
  where %1 >nul 2>nul
  if errorlevel 1 (
    echo   "%2" not found. Install it first.
    exit /b 1
  )
  exit /b 0

:is_running host port =
  curl -fsS -m 3 -o nul -w "%{http_code}" http://localhost:%2%/api/health >nul 2>nul
  if errorlevel 1 (
    exit /b 1
  )
  exit /b 0

:parse_dotnet_pid =
  tasklist /fi "imagename eq dotnet.exe" /fi "windowtitle eq %*.*" /fo csv 2>nul | find /i "dotnet.exe" >nul 2>nul
  if errorlevel 1 exit /b 1
  set "line="
  for /f "tokens=*" %%L in ('tasklist /fi "imagename eq dotnet.exe" /v /fo csv 2>nul ^| findstr /i "dotnet.exe"') do set "line=%%L"
  echo %line%
  exit /b 0

:: ---------- stop ----------
if /i "%~1"=="stop" (
  echo Stopping %APP_NAME%...

  :: 1. the web app
  for /f "tokens=2 delims=," %%p in ('tasklist /fi "imagename eq dotnet.exe" /fi "windowtitle eq LearningHub.*" /fo csv 2^>nul ^| findstr /i "dotnet.exe"') do (
    set "pid=%%~p"
    echo   dotnet PID %pid%
    taskkill /pid !pid! /f >nul 2>nul
  )
  taskkill /im dotnet.exe /fi "windowtitle eq LearningHub.*" /f >nul 2>nul

  :: 2. node (vite dev server if it is still around)
  taskkill /im node.exe /f >nul 2>nul

  :: 3. docker compose down (keeps MySQL volume, stops containers)
  if not "%LEARNINGHUB_SKIP_DOCKER%"=="1" (
    where docker >nul 2>nul
    if not errorlevel 1 (
      echo   stopping Docker containers...
      call docker compose down >nul 2>nul
    )
  )

  echo.
  echo %APP_NAME% stopped.
  goto :end
)

if /i "%~1"=="restart" (
  call :stop_internal
  echo.
  echo Restarting %APP_NAME%...
  call :start_internal
  goto :end
)

:: ---------- start ----------
if /i "%~1"=="" (
  call :start_internal
  goto :end
)

echo Unknown command: %~1
echo.
echo Usage:
echo   start.cmd            start %APP_NAME%
echo   start.cmd stop       stop it
echo   start.cmd restart    stop, then start again
echo.
goto :end

:start_internal
echo.

:: 1. prerequisites
where dotnet >nul 2>nul || (
  echo.
  echo Learning Hub needs the .NET 10 SDK: https://dotnet.microsoft.com/download
  pause
  exit /b 1
)

where node >nul 2>nul || (
  echo.
  echo Learning Hub needs Node.js 20 or newer: https://nodejs.org
  pause
  exit /b 1
)

:: 2. config
if exist ".env" (
  for /f "usebackq eol=# tokens=1,* delims==" %%A in (".env") do (
    if not "%%~A"=="" set "%%~A=%%~B"
  )
)

if not "%LEARNINGHUB_SKIP_DOCKER%"=="1" (
  where docker >nul 2>nul || (
    echo.
    echo Docker was not found. Install Docker Desktop, or run your own MySQL and set
    echo LEARNINGHUB_DB plus LEARNINGHUB_SKIP_DOCKER=1 in .env.
    pause
    exit /b 1
  )

  echo Starting the local MySQL container...
  call docker compose up -d
  if errorlevel 1 (
    echo.
    echo MySQL could not start. Is Docker Desktop running?
    pause
    exit /b 1
  )
)

:: 3. build the client once
pushd client
if not exist node_modules (
  echo Installing client dependencies...
  call npm install --no-audit --no-fund
  if errorlevel 1 (
    popd
    pause
    exit /b 1
  )
)
echo Building the interface...
call npm run build
if errorlevel 1 (
  popd
  pause
  exit /b 1
)
popd

:: 4. launch
echo.
echo %APP_NAME% is starting at http://localhost:%PORT%
echo Press Ctrl+C to stop. The database keeps running in Docker.
echo.

start "LearningHub" /min dotnet run --project %DOTNET_PROJECT%
endlocal
exit /b 0

:stop_internal
echo Stopping %APP_NAME%...

taskkill /im dotnet.exe /fi "windowtitle eq LearningHub.*" /f >nul 2>nul
taskkill /im node.exe /f >nul 2>nul

if not "%LEARNINGHUB_SKIP_DOCKER%"=="1" (
  where docker >nul 2>nul && (
    echo   stopping Docker containers...
    call docker compose down >nul 2>nul
  )
)
exit /b 0

:end
endlocal
