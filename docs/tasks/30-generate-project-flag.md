# 30 — `generate --project` (directory and file forms)

**Traces to:** L2-001

## Goal
`--project <dir>` and `--project <path-to-ng-package.json>` both work.

## Failing test
Two tests. First passes `--project <tempLibDir>`. Second passes `--project <tempLibDir>/ng-package.json`. Both assert exit `0` and output file written.

## Implementation
In command handler: if the provided path is a file, treat its directory as the start; else treat as start directory. Hand off to locator.

## Done when
- Both tests green.
