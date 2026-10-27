"""Every valid fixture parses into its generated pydantic model and dumps back to the same JSON (nulls dropped)."""
import importlib
import re
import sys
from datetime import datetime

import pytest
from conftest import ROOT, fixtures, load, schema_key, schema_paths

sys.path.insert(0, str(ROOT / "python" / "src"))
TIMESTAMP = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$")


def model_for(key, schema):
    module = importlib.import_module("blocwerk_contracts.models." + key.replace("/", ".").replace("-", "_"))
    return getattr(module, schema["title"])


def canonical(value):
    """Nulls dropped (absent and null mean the same), timestamps compared as instants ('Z' == '+00:00')."""
    if isinstance(value, dict):
        return {k: canonical(v) for k, v in value.items() if v is not None}
    if isinstance(value, list):
        return [canonical(v) for v in value]
    if isinstance(value, str) and TIMESTAMP.match(value):
        return datetime.fromisoformat(value.replace("Z", "+00:00"))
    return value


CASES = [(schema_key(p), f) for p in schema_paths() if not schema_key(p).startswith("layouts/")
         for f in fixtures(schema_key(p), "valid")]


@pytest.mark.parametrize(("key", "path"), CASES, ids=[f"{k}/{p.name}" for k, p in CASES])
def test_fixture_round_trips(key, path):
    model = model_for(key, load(ROOT / "schemas" / f"{key}.schema.json"))
    doc = load(path)
    dumped = model.model_validate(doc).model_dump(mode="json", by_alias=True, exclude_none=True, exclude_unset=True)
    assert canonical(dumped) == canonical(doc)
