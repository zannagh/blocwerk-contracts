# Local entry points; CI runs the same commands (.github/workflows/ci.yml).
PYTHON ?= .venv/bin/python

.PHONY: venv generate check-generated test test-python test-dotnet

venv:
	python3 -m venv .venv
	.venv/bin/pip install -q -r requirements-dev.txt

generate:
	PYTHON=$(PYTHON) scripts/generate.sh

check-generated: generate
	git add -N python/src dotnet/Blocwerk.Contracts.Compute/Generated
	git diff --exit-code -- python/src dotnet/Blocwerk.Contracts.Compute/Generated

test: test-python test-dotnet

test-python:
	$(PYTHON) -m pytest -q tests

test-dotnet:
	dotnet test dotnet/Blocwerk.Contracts.slnx -c Release
