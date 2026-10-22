#!/usr/bin/env bash
set -e
cd "$(dirname "$0")"
echo "=== stopping ==="
./start.sh stop
sleep 3
echo "=== start ==="
LEARNINGHUB_DB="Server=127.0.0.1;Port=3306;Database=learninghub;User ID=learninghub;Password=learninghub_dev;" ./start.sh start
sleep 30
echo "=== health ==="
curl -fsS --max-time 6 -o /dev/null -w "http=%{http_code}\n" http://localhost:5180/api/health 2>/dev/null || echo "NOT_RUNNING"
