# blocwerk-contracts

The protocol between the [Blocwerk](https://github.com/zannagh/blocwerk) app and its compute runners and workers,
as machine-checkable contracts: OpenAPI for the two HTTP APIs, JSON Schemas for every payload and job document,
golden fixtures from real payloads, and generated C# records and pydantic models.

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
| `dotnet/` | NuGet package `Blocwerk.Contracts.Compute` (generated records + `ContractJson.Options`) |
| `python/` | wheel `blocwerk-contracts` (generated pydantic v2 models) |

## Consuming it

**Recommended: the pinned packages.** They carry exactly one contract version, CI proves they match the schemas, and
the version is visible in each consumer's dependency file (`Directory.Packages.props`, `requirements*.txt`), so the
N/N-1 rule can be checked by reading versions instead of submodule SHAs.

- .NET (the app): add the GitHub Packages feed and pin the version.

  ```xml
  <!-- nuget.config: <add key="zannagh" value="https://nuget.pkg.github.com/zannagh/index.json" /> -->
  <PackageVersion Include="Blocwerk.Contracts.Compute" Version="0.1.0" />
  ```

  ```csharp
  var claim = JsonSerializer.Deserialize<RunnerClaim>(json, ContractJson.Options);
  if (claim.Kind == KindValues.DetectHolds) { … }
  ```

  GitHub Packages needs a token with `read:packages` even for public packages (in CI: `GITHUB_TOKEN`).

- Python (workers, runners): install the wheel from the GitHub release.

  ```text
  blocwerk-contracts @ https://github.com/zannagh/blocwerk-contracts/releases/download/v0.1.0/blocwerk_contracts-0.1.0-py3-none-any.whl
  ```

  ```python
  from blocwerk_contracts.models.runner.claim import RunnerClaim
  claim = RunnerClaim.model_validate(doc)
  ```

**Conformance tests in a consumer** need the schemas and fixtures, not only the models. Fetch them at the pinned tag
(the release attaches `contracts-<version>.tar.gz` with `schemas/`, `openapi/` and `fixtures/`) and validate the
consumer's own writers' output against the schemas, and its readers against `fixtures/*/valid`.

A git submodule (the pattern claudtty uses) also works and needs no package feed, but it pins a SHA rather than a
version, drags generated code for both languages into every consumer, and makes it easy to depend on an unreleased
commit. Prefer it only for experiments.

## Working on it

```sh
make venv            # Python tooling (.venv), pinned in requirements-dev.txt
make test            # schemas + fixtures + OpenAPI + pydantic round-trips, and the .NET round-trips
make generate        # regenerate python/src/.../models and dotnet/.../Generated after a schema change
make check-generated # what CI runs: regenerate and fail on any diff
```

A change to a schema needs: the schema, at least one `valid/` and one `invalid/` fixture that exercise it,
regenerated code, a note in `docs/protocol.md`, and the right version bump (`docs/versioning.md`).

Generators: [datamodel-code-generator](https://github.com/koxudaxi/datamodel-code-generator) for pydantic and
[NJsonSchema](https://github.com/RicoSuter/NJsonSchema) (through `tools/GenerateCSharp`, which maps string enums to
string constants and setters to `init`) for C#. Both are pinned, so generation is reproducible.

## Releases

Pushing a tag `vX.Y.Z` runs `.github/workflows/release.yml`: it tests, packs `Blocwerk.Contracts.Compute` X.Y.Z to
GitHub Packages, builds the wheel, and creates the GitHub release with the wheel and the contracts tarball.

## License

AGPL-3.0, like Blocwerk. See [LICENSE](LICENSE).
