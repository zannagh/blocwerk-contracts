#!/usr/bin/env bash
# Regenerates the checked-in models from schemas/: pydantic (python/src/blocwerk_contracts/models) and
# C# records (dotnet/Blocwerk.Contracts.Compute/Generated). Generator versions are pinned (requirements-dev.txt,
# tools/GenerateCSharp), so the output is reproducible; CI fails when it differs from what is committed.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
python="${PYTHON:-python3}"

"$python" "$root/scripts/generate_python.py"
dotnet run --project "$root/tools/GenerateCSharp" -c Release -- "$root/schemas" "$root/dotnet/Blocwerk.Contracts.Compute/Generated"
