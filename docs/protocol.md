# Blocwerk compute protocol

How the Blocwerk app talks to the machines that do its heavy work. There are two APIs:

| API | direction | who | spec |
|---|---|---|---|
| **Runner API** (runner protocol 1.1) | the runner pulls from the app | 3D/compute runners (`gpurunner`, in a home, on a GPU box, someone else's machine) | [`openapi/runner-api.yaml`](../openapi/runner-api.yaml) |
| **Compute job API** (`blocwerk-compute/1`) | the app pushes to a worker | `wall-geometry`, `splat-worker` (next to the app, or reachable by URL) | [`openapi/compute-job-api.yaml`](../openapi/compute-job-api.yaml) |

The payloads of both are JSON Schemas in [`schemas/`](../schemas); zip bundles and results are described as entry
listings in [`schemas/layouts/`](../schemas/layouts). Golden examples are in [`fixtures/`](../fixtures). How versions
change: [`versioning.md`](versioning.md). Where the code and older docs disagree: [`code-vs-docs.md`](code-vs-docs.md).

This document replaces blocwerk's `docker/compute-jobs-protocol.md` (which covered the compute job API and the runner
envelope) and the runner sections of `docker/splat-worker/README.md`. It describes what the code does as of blocwerk
`main` at `b3bde069` plus `staging-o7` at `373676d3` (runner protocol 1.1).

---

## 1. Runner API

A runner connects **out** to the app; there is no inbound port and no route into the runner's network. The app never
pushes: everything a runner does starts with its own request.

### 1.1 Authentication and identity

- `Authorization: Bearer bwr_…`: the runner's key. The owner creates it in the app and sees it once. The key names a
  runner, never a person. Unknown, revoked, or the owner's account gone: `401` (no body). The Python runner exits with
  code 3 on a `401`.
- `X-Blocwerk-Claim: <token>` (1–64 characters, random per runner **process**): the claim records it, and later
  job-scoped calls with a different token are refused, so a second process with the same key cannot train the same
  job. A runner that sends none is not checked.
- Rate limits: per address before authentication (burst 120, 40 per 10 s), per runner after it (burst 60, 20 per
  10 s). A throttled call gets `429` with `Retry-After: 10`.
- Every job-scoped call re-checks eligibility (runner not revoked, owner still administers the wall, approval still
  valid). A runner that lost the right gets `410` at its next call.
- The whole API is absent when the server runs with `RUNNERS__MODE=off`.

### 1.2 Lifecycle

```
hello ──► claim (long-poll ~25 s) ──204──► claim …
            │200
            ▼
          GET bundle (Range, sha256) ─► progress … (every ≤ lease, renews it) ─► PUT result ─► 200 {ok}
                                        │                                   └► PUT preview (splat only)
                                        └► POST fail (failure, shutdown, pause, unreachable)
```

1. **`POST /api/runners/hello`** ([`runner/hello`](../schemas/runner/hello.schema.json)): hardware, trainer, highest
   quality, memory budget, `capabilities`, `paused`. At start, after reconnecting, and every 30 s while paused. The
   answer ([`runner/hello-response`](../schemas/runner/hello-response.schema.json)) has `runnerId`, `name`,
   `pollSeconds` and `pauseAware`.
2. **`POST /api/runners/claim`** (optional body [`runner/claim-request`](../schemas/runner/claim-request.schema.json):
   `maxQuality`, `maxBundleBytes`): `200` with a [`runner/claim`](../schemas/runner/claim.schema.json), or `204` after
   about 25 s. The server picks the oldest queued job the runner may take: its own walls first, then walls that
   approved it as a shared runner (only while that wall has no own runner online). Filters: capability (§1.4), quality
   (`min(hello.maxQuality, claim.maxQuality)`, unknown counts as `high`), bundle size, memory, and a job this runner
   failed is left to another idle runner for a while. A runner that already holds a job and was silent long enough to
   have restarted gets that job back (`reattached: true`). A claim clears the runner's paused flag.
3. **`GET /api/runners/jobs/{id}/bundle`**: the input zip; `Range` supported (resume). Check `bundleBytes` and
   `bundleSha256`. A bundle missing on the server fails the job (`410 over`).
4. **`POST /api/runners/jobs/{id}/progress`** ([`runner/progress`](../schemas/runner/progress.schema.json)): the lease
   heartbeat (lease 5 min; the Python runner sends every 30 s). `stage: "download"` / `"upload"` are fixed meanings
   (`upload` extends the deadline so a big upload is not cut off); any other stage reads as the kind's working stage.
   Answer: [`runner/progress-response`](../schemas/runner/progress-response.schema.json) (`cancel` is always false; a
   cancelled job answers `410`).
5. **`PUT /api/runners/jobs/{id}/result`**: the result, per kind (§1.4). `Content-Encoding: gzip` allowed (the cap
   counts decoded bytes; a body that inflates past a sane ratio is stopped). Optional header `X-Blocwerk-Stats`
   ([`runner/upload-stats`](../schemas/runner/upload-stats.schema.json)): a flat JSON object ≤ 8 KiB; only `retries`
   may nest; anything else nested drops the header (not the upload). Minimum rate 16 KiB/s after 30 s, at most 2 h
   (`RUNNERS__MAXUPLOADMINUTES`). One upload per job at a time (`409`), at most 2 server-wide (`429`), and only while
   the server has 5 GiB free (`507`). The first complete, valid upload completes the job; a later upload for the same
   job gets `410 over`.
6. **`PUT /api/runners/jobs/{id}/preview?step=&total=`** (`splat` claims with `previews: true` only): an intermediate
   `.ply`/`.spz`, ≤ 1 GiB. `422` means "not taken" (off, used up, too early or too soon, or something newer is there):
   carry on training.
7. **`POST /api/runners/jobs/{id}/fail`** ([`runner/failure`](../schemas/runner/failure.schema.json)): hands the job
   back. How it is counted, first match wins:

   | flags | meaning | cost |
   |---|---|---|
   | `shutdown` + `pause` | the owner paused the runner mid-job | free (≤ `RUNNERS__MAXPAUSES` 50 per job; after 5 pauses without a newer checkpoint they count as shutdowns); the runner stays online, paused |
   | `shutdown` | the runner is stopping (SIGTERM, log-off); `checkpointStep` = newest kept checkpoint | free when the checkpoint advanced, else counts against a budget |
   | `unreachable` | gave up because the server/network stayed away | a lost lease (≤ 10), not an attempt |
   | `retryable` | may succeed on another try or runner | an attempt (≤ 3) |
   | none | fails for good | the job fails |

**Refusals on job-scoped calls** ([`runner/problem`](../schemas/runner/problem.schema.json), `application/problem+json`):

| status | meaning | runner does |
|---|---|---|
| `404` (no body) | not this runner's job, or unknown | drop it |
| `410` `reason: "requeued"` | it was yours, went back to the queue (lease lost, eligibility) | stop; keep checkpoints, it may come back |
| `410` `reason: "over"` | over for good (cancelled, failed, finished) | stop; drop checkpoints |
| `408`, `409`, `429`, `5xx` | transient | retry with back-off (`Retry-After`) |
| `400` | upload interrupted | retry |
| `413`, `415`, `422` | this upload is not acceptable | do not resend the same bytes |

Older servers send `410` without `reason`: treat it as `requeued`.

**Deadlines.** A claim may run `RUNNERS__MAXJOBHOURS` (6 h; `splat-prepare` 10 h). A lease not renewed within 5 min
puts the job back in the queue.

### 1.3 Pause

A paused runner makes no claims and says hello every 30 s with `paused: true`, so the app shows it as paused, not
offline, and routes no work to it. "Pause now" hands the current job back with `fail {shutdown, pause,
checkpointStep}`. Only servers that answer hello with `pauseAware: true` understand this; against an older server a
runner says `paused` once and then stays silent while paused (it shows offline there).

### 1.4 Job kinds

A runner is offered a kind only when its hello lists the capability of the same name. The server keeps only names it
knows; a runner with no list (or no known name) counts as `splat` only. This is how old runners and new servers (and
the other way round) stay compatible (see [`versioning.md`](versioning.md)).

| kind = capability | since | gate besides the capability | bundle (`GET …/bundle`) | result (`PUT …/result`) |
|---|---|---|---|---|
| `splat` | 1.0 | quality ≤ runner's | [`layouts/splat-bundle`](../schemas/layouts/splat-bundle.schema.json): `train.json` ([`jobs/splat/train`](../schemas/jobs/splat/train.schema.json)), optional `zones.json` ([`jobs/splat/zones`](../schemas/jobs/splat/zones.schema.json)), `dataset/images/**`, `dataset/sparse/**` (COLMAP) | the trained scene: one `.ply` or `.spz`, ≤ 2 GiB |
| `textures` | 1.0 | `requiredMemoryMb` ≤ hello `texturesMemoryMb` | [`layouts/textures-bundle`](../schemas/layouts/textures-bundle.schema.json): `textures-job.json` ([`jobs/textures/textures-job`](../schemas/jobs/textures/textures-job.schema.json)), `geometry.json`, `photos/<camera>.jpg\|png` | zip, [`layouts/textures-result`](../schemas/layouts/textures-result.schema.json): `textures.json` ([`jobs/textures/textures-manifest`](../schemas/jobs/textures/textures-manifest.schema.json)) + `facet_*` files |
| `detect-holds` | 1.0 | — | [`layouts/holds-bundle`](../schemas/layouts/holds-bundle.schema.json): `holds-job.json` ([`jobs/detect-holds/holds-job`](../schemas/jobs/detect-holds/holds-job.schema.json)), `model.onnx`, `photos/<camera>.jpg\|png` | JSON [`jobs/detect-holds/holds-result`](../schemas/jobs/detect-holds/holds-result.schema.json), ≤ 64 MiB |
| `register-panels` | 1.0 | — | [`layouts/panels-bundle`](../schemas/layouts/panels-bundle.schema.json): `register-job.json` ([`jobs/register-panels/register-job`](../schemas/jobs/register-panels/register-job.schema.json)), `photos/<camera>.jpg`, `panels/<id>r<rev>.jpg`, ≤ 8 GiB | JSON [`jobs/register-panels/panels-result`](../schemas/jobs/register-panels/panels-result.schema.json), ≤ 4 MiB |
| `splat-prepare` | 1.1 | `requiredMemoryMb` (6144) ≤ hello `memoryBudgetMb` | runner envelope (§2) | envelope result: `bundle.zip`, `prepared.json`, `sparse.zip` |
| `splat-finish` | 1.1 | `requiredMemoryMb` (3072) ≤ hello `memoryBudgetMb` | runner envelope (§2) | envelope result: `wall.splat`, `wall.spz`, `wall.raw.spz`, `frame.json` |

Kinds other than `splat` are claimed at quality `draft` and never get previews. The server rebuilds every bundle it
sends: entries are allow-listed, photos are metadata-stripped (no GPS, no serials) and stored uncompressed.

**`splat`.** The training bundle is the splat worker's `splat-prepare` `bundle.zip`, rebuilt by the server
(`RunnerBundle`): at most 20000 entries, 8 GiB uncompressed, 256 MiB per entry. The server accepts any subset of the
known `train.json` keys; the runner needs all of them (the schema is what both accept). A runner applies `zones.json`
only when `params.wallZones` is true (or its own `SPLAT_WALL_ZONES=1`).

**`detect-holds`.** The runner replays the app's tiled YOLO detection box for box. Boxes are
`[left, top, right, bottom, confidence, label]` in integer pixels; the server turns them into holds itself (centres
outside the photo, mat false positives and `volume` boxes dropped). The app waits 10 min for a claim and 180 min
overall (`RUNNERS__HOLDSCLAIMMINUTES`, `RUNNERS__HOLDSMAXMINUTES`), then detects in the app.

**`register-panels`.** `register-job.json` lists the capture's solved cameras (wall world, mm) that have a photo in the
bundle, and the panel photo names `<panel id, 32 hex>r<photo revision>`. A pose is COLMAP RADIAL
`params: [f, cx, cy, k1, k2]` plus `R`, `t`; a panel that does not register is left out.

---

## 2. Runner envelope (runner protocol 1.1)

How a `splat-prepare` / `splat-finish` request and its result cross the runner API, so a server without a splat
worker can have a runner do the CPU halves. The runner runs the same job function the HTTP worker runs. Both
directions are flat zips. Server: `RunnerComputeEnvelope.cs`, `RunnerComputeResult.cs`; runner:
`gpurunner/compute_envelope.py`.

### 2.1 Input: `parts.json` + the files

The multipart request the HTTP worker would have received, as one zip
([`layouts/envelope-input`](../schemas/layouts/envelope-input.schema.json),
[`envelope/parts`](../schemas/envelope/parts.schema.json)):

```json
{"version": 1, "service": "splat", "kind": "splat-prepare", "parts": [
  {"name": "photos", "file": "IMG_0001.jpg"},
  {"name": "photos", "file": "vf_0001.jpg"},
  {"name": "photos", "file": "a00.jpg"},
  {"name": "geometry", "json": {"cameras": [], "segments": []}},
  {"name": "options", "json": {"quality": "high"}}]}
```

- `parts.json` is the first entry; every file entry is **stored** (no compression). At most 1431 entries.
- Each part has `name` and exactly one of `file` (a zip entry of that name, also the part's file name) or `json` (the
  field's value inline). `parts` keep the order of the original request; `photos` repeats.
- A `file` is a plain name: no `/`, `\`, `:` or control characters, not `.` or `..`, not `parts.json`, ≤ 255
  characters, unique ignoring case. Names are otherwise unrestricted, so a reader treats them as untrusted.
- Part names: `splat-prepare` takes `photos` (photos, `vf_…` video frames and `a00…` anchor photos all travel as
  `photos`), `geometry`, `options`; `splat-finish` takes `prepared` (json), `splat` (`splat.ply` or `splat.spz`),
  `trainStats` (json). A runner refuses any other part name.

### 2.2 Result: `status.json` + the files it lists

A zip ([`layouts/envelope-result`](../schemas/layouts/envelope-result.schema.json)) of `status.json`, exactly the
`GET /v1/jobs/{id}` body ([`worker/job-status`](../schemas/worker/job-status.schema.json)) of the finished job with
`status: "succeeded"` and `result.files` (`{name, url}` or bare names), plus every file it lists, stored uncompressed.
The server refuses the upload (`422`, the claim stays) when it is not a zip, has more than 64 entries, an entry name
is not `[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}` with `.zip .json .splat .spz .ply`, a name repeats (ignoring case), a file
other than `status.json` is compressed, an entry is not listed or a listed file is missing, `status.json` is over
4 MiB or not JSON, or the entries exceed the cap (`splat-prepare`: `MaxBundleBytes` + 576 MiB; `splat-finish`: 2 GiB).
A failed run is reported with `fail`, never as a result.

---

## 3. Compute job API (`blocwerk-compute/1`)

One protocol, implemented once in blocwerk `docker/compute-jobs-py` and served by:

| worker | kinds | elsewhere (`501`) |
|---|---|---|
| `wall-geometry` | `solve`, `solve-sfm`, `textures` | `splat` |
| `splat-worker` | `splat`, `splat-prepare`, `splat-finish` | `solve`, `solve-sfm`, `textures` |

The app knows a worker only as a **base URL plus an optional API key**; where it runs changes nothing in the protocol.
Any other kind answers `404`.

### 3.1 Auth

- With `COMPUTE_API_KEY` set, every endpoint except `GET /health` needs `Authorization: Bearer <key>`; otherwise `401`
  with `WWW-Authenticate: Bearer`. Constant-time compare, never logged.
- A worker refuses to listen beyond loopback without a key (exit code 2) unless `ALLOW_OPEN_BIND=1`. External workers
  must set a key and should sit behind TLS.
- The key and the callback secret are removed from the worker's environment at start; job processes and their tools
  get a minimal allow-listed environment.
- There is no `/docs`, `/redoc` or `/openapi.json` on a worker.

### 3.2 Endpoints

| endpoint | answer |
|---|---|
| `GET /health` (never authenticated) | [`worker/health`](../schemas/worker/health.schema.json): `status` `ok`\|`degraded`, `service`, `protocol`, `version`, `kinds`; the splat worker adds `prepareOutputs` and, when it trains ultra itself, `maxQuality` |
| `GET /v1/info` | [`worker/info`](../schemas/worker/info.schema.json): health plus `gitSha`, `auth`, `jobs {queued, running}`, `limits` (the app reads `limits.maxPhotos` before it starts a capture), `tools` |
| `POST /v1/jobs/{kind}` | `202` [`worker/job-accepted`](../schemas/worker/job-accepted.schema.json) |
| `GET /v1/jobs/{id}` | [`worker/job-status`](../schemas/worker/job-status.schema.json) |
| `DELETE /v1/jobs/{id}` | the status; queued → `cancelled` at once, running → killed within about a second, finished → unchanged |
| `GET /v1/jobs/{id}/files/{name}` | a file listed in `result.files` |

Errors are [`worker/error`](../schemas/worker/error.schema.json) `{detail}`: `400` malformed body (JSON `NaN` /
`Infinity` included), bad file name, duplicate or refused `callbackUrl`; `401`; `404` unknown kind or job; `413` body,
photo, count or pixels too large; `422` well-formed but invalid (unknown or out-of-range options included); `429` queue
full; `501` kind served by the other worker; `503` (splat worker) its tools are missing, or it runs in CPU mode and was
asked to train.

Photos are parsed while streaming and cleaned in memory (EXIF orientation applied, re-encoded without metadata); the
uploaded bytes never reach the worker's disk. A photo whose header claims more than `MAX_IMAGE_MEGAPIXELS` (100) is
refused before decoding.

### 3.3 Status, lifetime

`status` is `queued | running | succeeded | failed | cancelled`; `progress` 0..1, monotone within a run, 1 on success;
`stage` / `stageDetail` for a label; `error` only when failed (≤ 400 characters, no server paths; unexpected failures
read `internal error (ref <id>)`); `result` only when succeeded, per kind; `result.files` = `[{name, url}]` with `url`
the path of the file endpoint (`/v1/jobs/{id}/files/{name}`).

Workers are single-instance with in-memory state. **Results and files expire `RESULT_TTL_S` (3600 s) after the job
finished, and everything is lost on restart**: fetch promptly and treat `404` on a known job as "expired or restarted"
(submit again).

### 3.4 Progress callbacks (optional)

A job created with `callbackUrl` (top-level JSON field, or form field) gets the status JSON POSTed on every status
change, with `X-Blocwerk-Signature: sha256=<hex HMAC-SHA256 of the raw body, key COMPUTE_CALLBACK_SECRET>`.

- Verify the signature over the raw bytes, in constant time, before trusting anything. Test vector: body
  `{"jobId":"abc","status":"succeeded"}`, secret `s3cret` →
  `sha256=8498b6c0d99f7e79edbb127d423ecbc4aeec6cf6696247b164d40e527b298e60`.
- Best effort: retries after 1 s, 3 s, 9 s (5 s timeout each), from a background thread; never delays the job. Order
  across retries is not guaranteed: use `updatedAt` / `status`. Without `COMPUTE_CALLBACK_SECRET` no callbacks are sent.
- Polling stays the primary path.
- SSRF guard: `http`/`https` only, no credentials in the URL, the host is resolved once and every address must be
  allowed, the POST goes to that pinned address, redirects are not followed. Never: link-local / metadata
  (`169.254.0.0/16`, `fe80::/10`, `fd00:ec2::254`, `100.100.100.200`, `168.63.129.16`), unspecified, multicast,
  reserved. Loopback / private / CGNAT / unique-local only with `CALLBACK_ALLOW_PRIVATE=1` or a host in
  `CALLBACK_ALLOWED_HOSTS`. A forbidden IP literal is a `400` at submission; a name resolving to a forbidden address is
  dropped at send time.

### 3.5 Kinds

| kind | body | result |
|---|---|---|
| `solve` | JSON: the solve request document (blocwerk `docker/wall-geometry/README.md`) | `result.geometry`, the wall-geometry document; file `wall-geometry.json` |
| `solve-sfm` | multipart: `request` (JSON), `sparse` (splat-prepare's `sparse.zip`, ≤ `SFM_MAX_SPARSE_MB`) | the wall-geometry document from a markerless capture |
| `textures` | multipart: `geometry` (solved document), `photos` (file name = camera `image` + `.jpg/.jpeg/.png`, size must match the solved camera), optional `options` ([`texturesOptions`](../schemas/jobs/textures/textures-job.schema.json): `mmPerPx`, `maxSidePx`, `extraMarginMm`, `jpegQuality`, `blendViews`) | [`textures-manifest`](../schemas/jobs/textures/textures-manifest.schema.json) facets; files `facet_<id>.jpg`, `facet_<id>_mask.png`, `facet_<id>_source.json`, `textures.json` |
| `splat` | multipart: `photos` (3–400), optional `geometry`, optional `options` ([`worker/splat-options`](../schemas/worker/splat-options.schema.json)) | `frame`, `stats`; files `wall.splat`, `wall.spz`, `frame.json` (+ `wall.raw.spz` with clean-up) |
| `splat-prepare` | as `splat`, plus anchor photos | `bundle.zip` (the runner's `splat` bundle), `prepared.json` (server-only state), `sparse.zip` |
| `splat-finish` | multipart: `prepared` (JSON), `splat` (`splat.ply` / `splat.spz`, ≤ `SPLAT_MAX_RESULT_MB`), optional `trainStats` | the same files as `splat` |

Photo name conventions across the boundary: a stem starting with **`vf_`** (`vf_0001.jpg`, in video order) is an
auxiliary video frame (trained on, never used for alignment, not counted towards the minimum); **`a00`, `a01`, …** are
anchor photos (`splat-prepare` only; `splat` refuses them with `422`).

The app uses `splat-prepare` / `splat-finish` only when the worker's `/health` lists both kinds; it treats a worker as
able to prepare markerless captures only when `prepareOutputs` lists `sparse.zip` and `anchors`.
