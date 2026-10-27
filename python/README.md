# blocwerk-contracts (Python)

Pydantic v2 models of the Blocwerk runner API, compute-worker API and job documents, generated from the JSON Schemas
in [zannagh/blocwerk-contracts](https://github.com/zannagh/blocwerk-contracts). Do not edit `models/`; change the
schemas and run `scripts/generate.sh`.

```python
from blocwerk_contracts.models.runner.claim import RunnerClaim

claim = RunnerClaim.model_validate(response.json())
body = claim.model_dump(mode="json", by_alias=True, exclude_none=True)
```

Field names are snake_case with the wire names as aliases; dump with `by_alias=True`. Absent and `null` mean the same
everywhere in the protocol, so `exclude_none=True` is safe.
