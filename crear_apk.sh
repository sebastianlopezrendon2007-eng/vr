#!/usr/bin/env bash
set -euo pipefail
QUEST_PROYECTO="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec python3 "$QUEST_PROYECTO/tools/quest.py" compilar
