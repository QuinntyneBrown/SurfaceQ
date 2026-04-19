# 42 — Determinism: repeat run byte-identical

**Traces to:** L2-012

## Goal
Two consecutive `generate` runs against unchanged source produce byte-identical files.

## Failing test
Run `generate`, hash output (SHA-256). Run `generate` again, hash output. Assert equal.

## Implementation
No code change expected if renderer is already deterministic. Test guards against regression.

## Done when
- Test green.
