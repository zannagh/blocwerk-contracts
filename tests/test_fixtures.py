"""Every schema is a valid 2020-12 schema, has golden fixtures, accepts its valid ones and refuses its invalid ones."""
import pytest
from conftest import FIXTURES, fixtures, load, schema_key, schema_paths
from jsonschema import Draft202012Validator, FormatChecker

SCHEMAS = {schema_key(p): p for p in schema_paths()}


def validator(key):
    return Draft202012Validator(load(SCHEMAS[key]), format_checker=FormatChecker())


@pytest.mark.parametrize("key", sorted(SCHEMAS))
def test_schema_is_valid_and_has_fixtures(key):
    Draft202012Validator.check_schema(load(SCHEMAS[key]))
    assert fixtures(key, "valid"), f"{key}: add at least one fixtures/{key}/valid/*.json"
    assert fixtures(key, "invalid"), f"{key}: add at least one fixtures/{key}/invalid/*.json"


def test_every_fixture_folder_has_a_schema():
    folders = {p.parent.parent.relative_to(FIXTURES).as_posix() for p in FIXTURES.rglob("*.json")}
    assert folders <= set(SCHEMAS), f"fixtures without a schema: {sorted(folders - set(SCHEMAS))}"


VALID = [(k, p) for k in SCHEMAS for p in fixtures(k, "valid")]
INVALID = [(k, p) for k in SCHEMAS for p in fixtures(k, "invalid")]


@pytest.mark.parametrize(("key", "path"), VALID, ids=[f"{k}/{p.name}" for k, p in VALID])
def test_valid_fixture_passes(key, path):
    errors = sorted(validator(key).iter_errors(load(path)), key=str)
    assert not errors, "\n".join(f"{list(e.absolute_path)}: {e.message}" for e in errors[:5])


@pytest.mark.parametrize(("key", "path"), INVALID, ids=[f"{k}/{p.name}" for k, p in INVALID])
def test_invalid_fixture_fails(key, path):
    assert not validator(key).is_valid(load(path)), f"{path.name} should be refused by {key}"
