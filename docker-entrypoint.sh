#!/bin/sh
set -e

# Support Render / Cloud dynamic PORT environment variable
if [ -n "$PORT" ]; then
  export ASPNETCORE_URLS="http://0.0.0.0:${PORT}"
elif [ -z "$ASPNETCORE_URLS" ]; then
  export ASPNETCORE_URLS="http://0.0.0.0:8080"
fi

# Ensure keys directory exists with appropriate permissions
if [ ! -d "/app/App_Data/keys" ]; then
  mkdir -p /app/App_Data/keys 2>/dev/null || true
fi

exec dotnet KrishiLink.dll "$@"
