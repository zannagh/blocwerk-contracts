# Where the fixtures come from

`valid/` fixtures must pass their schema and round-trip through the generated C# records and pydantic models;
`invalid/` fixtures must fail their schema (each for one stated reason). Folder = schema path without
`.schema.json`.

**Captured** fixtures are real payloads produced by blocwerk's own code (blocwerk `main` `b3bde069`, `staging-o7`
`373676d3`), unedited. **Derived** fixtures are written from a producer in the code (the C# writer or the Python
runner) or a test literal, where running the producer needs a GPU, a database or an ONNX model.

| fixture | kind | source |
|---|---|---|
| `runner/hello/valid/python-runner-brush-mac.json` | captured | `gpurunner.caps.Capabilities()._doc()` on an Apple Silicon Mac |
| `runner/hello/valid/python-runner-paused-lite.json` | captured | `Capabilities.lite()` + `paused`, as the paused loop sends it |
| `runner/hello/valid/cuda-gsplat-runner.json`, `pre-capabilities-runner.json` | derived | `caps.py` document shape; a runner from before capabilities (`RunnerHello` fields only) |
| `runner/hello-response/*` | derived | `RunnerApiEndpoints.HelloAsync`; `tests/fake_server.py` (with and without `pauseAware`) |
| `runner/claim-request/valid/python-runner.json` | derived | `gpurunner/client.py` `claim()` |
| `runner/claim/valid/*` | derived | `GpuJobQueue.ClaimAsync` → `RunnerClaim` per kind; `server-before-kinds` = the 1.0 shape `fake_server.py` sends |
| `runner/claim/invalid/fake-server-job-id.json` | captured | `tests/fake_server.py` `FakeServer.job()` (non-GUID ids, see `docs/code-vs-docs.md` #16) |
| `runner/progress/valid/heartbeat-initial.json` | derived | `gpurunner/heartbeat.py` initial state |
| `runner/failure/valid/*` | derived | `gpurunner/client.py` `fail()` for pause, SIGTERM, unreachable, fatal |
| `runner/problem/valid/*` | derived | `RunnerApiEndpoints.Gone` / `TooManyRequests` (ASP.NET problem details) |
| `runner/upload-stats/valid/brush-run.json` | derived | `gpurunner/job.py` `trim_stats` over `train.py` stats |
| `jobs/splat/train/valid/profile-*.json` | captured | `splatworker.bundle.train_doc(profiles.resolve(q))` for every quality |
| `jobs/splat/zones/valid/attic-real-geometry.json` | captured | `splatworker.zones.spec()` on The Attic's real geometry (`test/real-data/attic/model.json`), `wallZones: true`; `toWorldMm` set to a scale-only matrix (a real one needs a COLMAP run) |
| `jobs/textures/textures-job/*` | derived | `RunnerTexturesBundle.JobDocument` |
| `jobs/textures/textures-manifest/valid/wall-geometry-synthetic.json` | captured | wall-geometry `POST /v1/jobs/textures` on its test scene (`tests/synthetic.py`), the `textures.json` file |
| `jobs/detect-holds/holds-job/valid/app-defaults.json` | derived | `RunnerHoldsBundle.JobDocument` with the app's default tiling |
| `jobs/detect-holds/holds-result/valid/two-photos.json` | derived | the shape `gpurunner/holds_detect.py` uploads and `RunnerHoldsResult` reads |
| `jobs/register-panels/register-job/valid/attic-cameras.json` | derived | `RunnerPanelsBundle.JobDocument` over three real solved cameras of The Attic |
| `jobs/register-panels/panels-result/valid/from-csharp-test.json` | derived | the literal in `test/Blocwerk.Core.Tests/RunnerPanelPosesTests.cs` |
| `worker/health/valid/*`, `worker/info/valid/*`, `worker/job-accepted/valid/*`, `worker/error/valid/*` | captured | wall-geometry and splat-worker through FastAPI's `TestClient` |
| `worker/job-status/valid/textures-succeeded.json` | captured | wall-geometry textures job status |
| `worker/job-status/valid/golden-result-*-status.json` | captured | `status.json` of blocwerk's contract goldens `docker/compute-jobs-py/tests/contract/result-*.zip` (written by the runner's `pack_result`) |
| `envelope/parts/valid/golden-envelope-*.json` | captured | `parts.json` of `docker/compute-jobs-py/tests/contract/envelope-*.zip` (written by `RunnerComputeEnvelope`) |
| `layouts/envelope-*/valid/golden-*.json` | captured | entry listings of the same golden zips |
| other `layouts/*/valid/*` | derived | the entry names the C# bundle writers produce |
| `worker/job-status/valid/splat-running.json`, `envelope-status-bare-names.json`, `worker/splat-options/*` | derived | `computejobs/jobs.py` `to_status`; the bare-name form `RunnerComputeResult` accepts; `splatworker/options.py` |

To re-capture: run the producers from a blocwerk checkout (wall-geometry and splat-worker requirements installed) and
copy the output here unedited.
