# Versioning, compatibility and capability negotiation

## What carries a version

| thing | version | where it is visible on the wire |
|---|---|---|
| this repository (schemas, OpenAPI, generated packages) | semver tag `vX.Y.Z` | the NuGet / wheel version consumers pin |
| runner protocol | `MAJOR.MINOR` (today **1.1**) | **not on the wire yet**: negotiated through `capabilities` and response fields (below) |
| compute job API | `blocwerk-compute/N` (today 1) | `GET /health` → `protocol` |
| each job document | `"version": N` inside the document | `train.json`, `textures-job.json`, `holds-job.json`, `register-job.json`, holds / panels results, `parts.json` (all 1 today; `prepared.json` is 2 and server-internal) |

The runner protocol history:

| runner protocol | what it added | blocwerk |
|---|---|---|
| 1.0 | hello / claim / bundle / progress / result / preview / fail; kinds `splat`, `textures`, `detect-holds`, `register-panels`; pause | `main` |
| 1.1 | kinds `splat-prepare`, `splat-finish`; the runner envelope; per-kind result caps | `staging-o7` (PRs #239, #240) |

## Rules

1. **Additive is minor.** A new optional field, a new kind, a new capability, a new optional part, a new enum value
   that old readers never receive (see 3): minor version of this repo, minor version of the protocol.
2. **Anything else is major**: removing or renaming a field, making an optional field required, narrowing a range,
   changing a meaning, changing a zip layout in a way an old reader refuses.
3. **Readers ignore what they do not know; writers never send a new value to a peer that has not said it understands
   it.** The server ignores unknown JSON fields and drops unknown capability names; a runner ignores unknown claim
   fields. New enum values (a new `kind`) only ever reach a runner that advertised them.
4. **Absent equals null** in every document. Writers may omit null fields; readers must treat both the same. (Some
   readers refuse an explicit `null` for an optional number, so the generated C# options leave nulls out.)
5. **The server supports protocol N and N-1.** Runners in the field (the Pi, other people's shared runners,
   `gpu-runner-prod`) are updated by their owners, not by a deploy, so the server must keep serving the previous minor
   and major until those runners are gone.
6. **Documents inside a job carry their own `version`.** A reader refuses a version it does not know (the runner's
   `train.json` reader does) rather than guessing. A breaking document change bumps that `version` and is paired with a
   new capability (rule 3), so only runners that understand it receive it.

## Capability negotiation (today)

There is no version number in `hello`. Compatibility is negotiated per feature:

| question | how it is answered |
|---|---|
| May this runner get kind K? | its hello lists capability K (equal to the kind name). The server stores only names it knows; no list or no known name means `splat` only. So a new kind is invisible to old runners, and a new runner's unknown capability is dropped by an old server. |
| Does it have the resources for this job? | per kind: `textures` → `requiredMemoryMb` ≤ hello `texturesMemoryMb`; `splat-prepare` / `splat-finish` → `requiredMemoryMb` ≤ hello `memoryBudgetMb`; `splat` → job quality ≤ `min(hello.maxQuality, claim.maxQuality)` (the server lowers `maxQuality` to what trainer and VRAM support); bundle size ≤ claim `maxBundleBytes`. |
| Does the server understand pausing? | the hello answer has `pauseAware: true`. Without it the runner does not send paused hellos. |
| Does the server know `reason` on 410? | `reason` present. Without it a runner treats a `410` as `requeued` (keeps checkpoints). |
| Was this claim made by an older server? | no `kind` → `splat`; no `previews` → false; no `requiredMemoryMb` → no memory gate. |
| Can this compute worker do X? | `GET /health` → `kinds` (the app splits a splat into prepare/finish only when both are listed), `prepareOutputs` (`sparse.zip`, `anchors`: markerless captures), `maxQuality` (ultra without a runner), `protocol`. |

## How a breaking change rolls out

The order is always: contracts first, then the server accepts both, then the clients move, then the old one goes.

1. **Contracts (minor):** add v2 *next to* v1: a new capability name (for example `splat@2` or `textures-v2`), a new
   document `version`, new schemas and fixtures. v1 stays untouched and tested.
2. **Server:** accepts and offers both. A job that needs v2 is only offered to runners that advertise the v2
   capability; everyone else keeps getting v1 jobs. Deploy the server **before** any runner advertises v2 (an older
   server would drop the unknown capability, which is safe, but nothing would use it).
3. **Runners / workers:** ship images that advertise v2 (and, while v1 jobs may still be queued, v1 too).
4. **Drop v1 (contracts major):** one server release after the last v1 runner is gone (check the runners list in the
   app: `RunnerVersion` and stored capabilities). Remove v1 schemas in a major release of this repository.

A field rename is the same dance: add the new field (minor), servers read both and write both, clients move, drop the
old field (major).

## Planned (not implemented): `protocol` in hello

The platform-split design (blocwerk `.me/design-2026-10-27-platform-split.md` §4.2, §6) plans a hello v2 that reports
the runner's protocol version, executor, free disk, wakeability and power state, and job requirements as a record
(`{capability, minVramMb?, minMemoryMb, executor[], protocol}`). When it lands it is additive (rule 1): an optional
`protocol` field in hello, ignored by today's servers; the server then derives N/N-1 support from it instead of from
capability names alone.
