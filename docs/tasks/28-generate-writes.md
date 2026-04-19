# 28 — `generate` writes the file

**Traces to:** L2-001

## Goal
Running `surfaceq generate` against a real fixture library writes a correct `public-api.ts` to the resolved entry path.

## Failing test
Fixture library with two source files, known expected output. Run `generate`. Assert the file exists at `src/public-api.ts`, contents exactly equal the expected fixture output, and exit code `0`.

## Implementation
Command handler chains: locator → reader → walker → sidecar → renderer → `File.WriteAllText(entry, output)`.

## Done when
- Test green.
