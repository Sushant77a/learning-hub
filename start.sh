#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

APP_NAME="LearningHub"
PORT=5180
DOTNET_PROJECT="src/LearningHub.Api"

usage() {
  echo
  echo "$APP_NAME  —  start / stop"
  echo
  echo "Usage:"
  echo "  ./start.sh       start $APP_NAME"
  echo "  ./start.sh stop  stop it"
  echo "  ./start.sh restart"
  echo
  exit 1
}

is_running() {
  curl -fsS -m 3 -o /dev/null "http://localhost:${1}/api/health" 2>/dev/null
}

stop() {
  echo "Stopping $APP_NAME..."

  # dotnet / Kestrel
  pkill -f "dotnet.*run.*${DOTNET_PROJECT}" 2>/dev/null || true
  pkill -f "dotnet.*LearningHub.Api" 2>/dev/null || true
  sleep 1
  pkill -9 -f "dotnet.*LearningHub.Api" 2>/dev/null || true

  # vite dev server, if it is still around
  pkill -f "node.*vite" 2>/dev/null || true

  # docker compose (keeps the MySQL volume)
  if [[ "${LEARNINGHUB_SKIP_DOCKER:-0}" != "1" ]] && command -v docker >/dev/null 2>&1; then
    echo "  stopping Docker containers..."
    ( cd "$DOTNET_PROJECT/.." && docker compose down ) >/dev/null 2>&1 || true
  fi

  echo
  echo "$APP_NAME stopped."
}

case "${1:-start}" in
  start)
    echo

    command -v dotnet >/dev/null 2>&1 || {
      echo "Learning Hub needs the .NET 10 SDK: https://dotnet.microsoft.com/download" >&2
      exit 1
    }
    command -v node >/dev/null 2>&1 || {
      echo "Learning Hub needs Node.js 20 or newer: https://nodejs.org" >&2
      exit 1
    }

    if [[ -f .env ]]; then
      set -a
      # shellcheck disable=SC1091
      source ./.env
      set +a
    fi

    if [[ "${LEARNINGHUB_SKIP_DOCKER:-0}" != "1" ]]; then
      if ! command -v docker >/dev/null 2>&1; then
        echo "Docker was not found. Install Docker, or run your own MySQL and set" >&2
        echo "LEARNINGHUB_DB plus LEARNINGHUB_SKIP_DOCKER=1 in .env." >&2
        exit 1
      fi

      echo "Starting the local MySQL container..."
      ( cd "$DOTNET_PROJECT/.." && docker compose up -d )
    fi

    cd client
    if [[ ! -d node_modules ]]; then
      echo "Installing client dependencies..."
      npm install --no-audit --no-fund
    fi
    echo "Building the interface..."
    npm run build
    cd ..

    echo
    echo "$APP_NAME is starting at http://localhost:$PORT"
    echo "Press Ctrl+C to stop. The database keeps running in Docker."
    echo

    nohup dotnet run --project "$DOTNET_PROJECT" > /tmp/learninghub.log 2>&1 &
    disown
    ;;

  stop)
    stop
    ;;

  restart)
    stop
    echo
    sleep 1
    echo "Restarting $APP_NAME..."
    shift
    exec ./start.sh start
    ;;

  *)
    usage
    ;;
esac
