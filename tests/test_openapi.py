"""Both OpenAPI documents are valid 3.1 and every $ref (into schemas/, with fragments) resolves."""
import pytest
import yaml
from conftest import OPENAPI, load
from openapi_spec_validator import validate
from openapi_spec_validator.readers import read_from_filename

DOCS = sorted(OPENAPI.glob("*.yaml"))


@pytest.mark.parametrize("path", DOCS, ids=[p.name for p in DOCS])
def test_openapi_document_is_valid(path):
    spec, base_uri = read_from_filename(str(path))
    validate(spec, base_uri=base_uri)


def refs(node):
    if isinstance(node, dict):
        if isinstance(node.get("$ref"), str):
            yield node["$ref"]
        for value in node.values():
            yield from refs(value)
    elif isinstance(node, list):
        for value in node:
            yield from refs(value)


@pytest.mark.parametrize("path", DOCS, ids=[p.name for p in DOCS])
def test_every_external_ref_resolves(path):
    doc = yaml.safe_load(path.read_text(encoding="utf-8"))
    external = {r for r in refs(doc) if not r.startswith("#")}
    assert external
    for ref in external:
        file, _, fragment = ref.partition("#")
        target = load((path.parent / file).resolve())
        for part in filter(None, fragment.split("/")):
            assert part in target, f"{ref}: no {part}"
            target = target[part]
