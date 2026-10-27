# Where the code and the existing docs disagree

Found while writing these contracts from the code (blocwerk `main` `b3bde069`, `staging-o7` `373676d3`). The schemas
follow the **code**. Nothing here was changed in blocwerk; each row is a follow-up there.

## Docs that do not match the code

| # | doc | says | code does |
|---|---|---|---|
| 1 | `docker/compute-jobs-protocol.md` kinds table | wall-geometry serves `solve`, `textures` | it also serves `solve-sfm` (`service/main.py`; documented only in `docker/wall-geometry/README.md`) |
| 2 | `docker/compute-jobs-protocol.md` `textures` options | `mmPerPx`, `maxSidePx`, `extraMarginMm`, `jpegQuality` | also `blendViews` (1–8, default 6; `wallgeometry/textures.py` `CLIENT_OPTIONS`); the wall-geometry README has it |
| 3 | `docker/compute-jobs-protocol.md` `textures` result | `facets[]: {facet, file, widthPx, heightPx, mmPerPx, bounds, photosUsed, coverage, markerCheck}` | also `maskFile`, `sourceFile`, `exposure`, `shading` per facet and a top-level `pixelConvention`; the app reads `maskFile` / `sourceFile` |
| 4 | `docker/compute-jobs-protocol.md` errors | `400, 401, 404, 413, 422, 429, 501` | the splat worker also answers `503` (tools missing, or a CPU-mode worker asked to train; `split_api.require_tools`) |
| 5 | `docker/compute-jobs-protocol.md` | a worker answers `501` for a kind another worker serves | only for the kinds in its `elsewhere` list: wall-geometry answers `404` for `splat-prepare` / `splat-finish` (only `splat` is listed) |
| 6 | `docker/compute-jobs-protocol.md` | `result.files[].url` is "relative to the worker's base URL" | it is an absolute path (`/v1/jobs/{id}/files/{name}`), so it breaks behind a path prefix |
| 7 | `docker/splat-worker/README.md` `options.quality` (and the `options.py` field comment) | `draft` \| `high` \| `max` | `ultra` is accepted too (`profiles.QUALITIES`) |
| 8 | `docker/splat-worker/README.md` `cropMarginMm` | a number, default 400 | an **integer** 0–5000 (`options._int`) |
| 9 | `docker/splat-worker/README.md` hold detection result | `boxes: [[left, top, right, bottom, confidence, label]]` | the server refuses non-integer pixel values (`RunnerHoldsResult.ParseBox`, `TryGetInt32`) and confidences outside 0..1 |
| 10 | `gpurunner/compute_envelope.py` docstring (staging-o7) | example `{"name": "geometry", "file": "geometry.json"}` | the server writes `geometry` as an inline `json` part (`envelope-prepare.zip` golden); the runner accepts both |
| 11 | platform-split design §6 ("hello carries the version"), the P0 brief | hello reports a protocol version; there is a `heartbeat` endpoint | hello has no version field (negotiation is per capability, see `versioning.md`); the heartbeat is `POST …/progress` |
| 12 | platform-split design §4.5 | a repeated result upload is a `200` no-op, keyed by `(jobId, attempt, claimToken)` | a second upload while one streams is `409`; after the job completed it is `410 over` |

## Code that disagrees with other code

| # | where | what |
|---|---|---|
| 13 | hello: `gpurunner/caps.py` vs `RunnerHello` | the runner sends `trainerVersion`; the server has no such field and ignores it (it only shows `brushVersion`) |
| 14 | `train.json`: `RunnerTrainOptions` (server) vs `bundle.profile_from_doc` (runner) | the server accepts any subset of the known keys and any scalar values; the runner requires every key, `version` 1, and ranges (`edge` 256–8192, `steps` 100–100000, `maxSplats` 1000–20M). A bundle the server passes can still be refused by the runner |
| 15 | `RunnerPanelPoses` | `observations` is capped at 100000 by reusing `MaxSidePx`; a panel with more observations fails the whole result |
| 16 | `docker/splat-worker/tests/fake_server.py` | job, wall and capture ids are `"j1"`, `"w1"`, `"c1"`; the server always sends GUIDs, so the runner tests never see a real id (fixture `runner/claim/invalid/fake-server-job-id.json`) |
| 17 | `textures-job.json` | the runner does not check `version` (`textures_bundle.py` reads `options` only), unlike the other job documents |
| 18 | claim | the Python runner asks for `maxBundleBytes` 8 GiB; the server's own cap is 6 GiB (`RUNNERS__MAXBUNDLEMB`), so the runner's limit never bites (harmless) |
