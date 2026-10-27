"""Shared paths and helpers for the conformance tests."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCHEMAS = ROOT / "schemas"
FIXTURES = ROOT / "fixtures"
OPENAPI = ROOT / "openapi"


def schema_paths():
    return sorted(SCHEMAS.rglob("*.schema.json"))


def schema_key(path):
    """schemas/runner/hello.schema.json -> runner/hello (also the fixture folder)."""
    return path.relative_to(SCHEMAS).as_posix().removesuffix(".schema.json")


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def fixtures(key, kind):
    return sorted((FIXTURES / key / kind).glob("*.json"))
