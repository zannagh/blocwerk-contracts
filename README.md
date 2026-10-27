# blocwerk-contracts

The protocol between the [Blocwerk](https://github.com/zannagh/blocwerk) app and its compute runners and workers,
as machine-checkable contracts: OpenAPI for the two HTTP APIs, JSON Schemas for every payload and job document,
and golden fixtures from real payloads.

It describes what exists; it does not add behaviour. Worker repositories (geometry, 3D generation, recognition) and
the app depend on a pinned version of it, so they can be released independently.

| | |
|---|---|
| [`docs/protocol.md`](docs/protocol.md) | the protocol: runner API, job kinds, runner envelope, compute job API |
| [`docs/versioning.md`](docs/versioning.md) | semver, N/N-1 support, capability negotiation, how a breaking change rolls out |
| [`docs/code-vs-docs.md`](docs/code-vs-docs.md) | where blocwerk's code and its older docs disagree (follow-ups) |
| [`openapi/runner-api.yaml`](openapi/runner-api.yaml) | `/api/runners/*` (runner protocol 1.1), OpenAPI 3.1 |
| [`openapi/compute-job-api.yaml`](openapi/compute-job-api.yaml) | the workers' `/health`, `/v1/info`, `/v1/jobs` (`blocwerk-compute/1`) |
| [`schemas/`](schemas) | JSON Schema 2020-12: `runner/`, `jobs/<kind>/`, `envelope/`, `worker/`, and zip `layouts/` |
| [`fixtures/`](fixtures) | `valid/` and `invalid/` examples per schema; provenance in [`fixtures/SOURCES.md`](fixtures/SOURCES.md) |

## Working on it

```sh
python3 -m venv .venv && .venv/bin/pip install -r requirements-dev.txt
.venv/bin/pytest -q tests   # every schema is valid, accepts its valid fixtures, refuses its invalid ones; OpenAPI is valid
```

A change to a schema needs the schema, at least one `valid/` and one `invalid/` fixture that exercise it, a note in
`docs/protocol.md`, and the right version bump (`docs/versioning.md`).

## License

AGPL-3.0, like Blocwerk. See [LICENSE](LICENSE).
