# 02 — `--help` output

**Traces to:** L2-021

## Goal
`surfaceq --help` and `surfaceq -h` print help listing the three subcommands (`generate`, `check`, `diff`) and exit `0`.

## Failing test
Assert stdout from `--help` contains the literal strings `generate`, `check`, `diff`, and exit code `0`. Repeat for `-h`.

## Implementation
Register empty placeholder subcommands `generate`, `check`, `diff` on the root command with a short description each. `System.CommandLine` prints help for free.

## Done when
- Test green.
- Running `surfaceq <sub> --help` also prints subcommand help (verified manually).
