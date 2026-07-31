#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="$ROOT_DIR/PhotoBooth-Linux-x64"

dotnet publish \
  "$ROOT_DIR/PhotoBooth.Linux/PhotoBooth.Linux.csproj" \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output "$OUTPUT_DIR"

printf '\nLinux build created at:\n%s\n' "$OUTPUT_DIR"
